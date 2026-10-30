using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class BorrowingServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(Book book, BookCopy copy, ApplicationUser student)> SeedBookAndStudentAsync(ApplicationDbContext context)
        {
            var student = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "student@test.com",
                Email = "student@test.com",
                FullName = "Alice Johnson"
            };
            var author = new Author { Name = "Andrew Hunt" };
            var category = new Category { Name = "Software Engineering" };
            var book = new Book
            {
                Title = "The Pragmatic Programmer",
                ISBN = "978-0135957059",
                Author = author,
                Category = category
            };
            var copy = new BookCopy
            {
                Book = book,
                CopyCode = "PRAG-001",
                Status = BookCopyStatus.Available
            };

            context.Users.Add(student);
            context.Authors.Add(author);
            context.Categories.Add(category);
            context.Books.Add(book);
            context.BookCopies.Add(copy);
            await context.SaveChangesAsync();

            return (book, copy, student);
        }

        [Fact]
        public async Task IssueBookAsync_LimitExceeded_BlocksBorrowing()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            // Seed 3 existing active borrowings (the limit)
            for (int i = 1; i <= 3; i++)
            {
                var extraCopy = new BookCopy { Book = book, CopyCode = $"PRAG-00{i + 1}", Status = BookCopyStatus.Borrowed };
                context.BookCopies.Add(extraCopy);
                context.Borrowings.Add(new Borrowing
                {
                    UserId = student.Id,
                    BookCopy = extraCopy,
                    IssuedAt = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14),
                    Status = BorrowingStatus.Active
                });
            }

            // Create an active reservation for the 4th copy
            var reservation = new Reservation
            {
                UserId = student.Id,
                BookId = book.Id,
                BookCopyId = copy.Id,
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var result = await borrowingService.IssueBookAsync(reservation.Id);

            Assert.False(result.Success);
            Assert.Contains("maximum borrowing limit", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task IssueBookAsync_ValidReservation_IssuesBookAndMarksCopyBorrowed()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            copy.Status = BookCopyStatus.Reserved;
            var reservation = new Reservation
            {
                UserId = student.Id,
                BookId = book.Id,
                BookCopyId = copy.Id,
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var result = await borrowingService.IssueBookAsync(reservation.Id);

            Assert.True(result.Success);
            var updatedCopy = await context.BookCopies.FindAsync(copy.Id);
            Assert.Equal(BookCopyStatus.Borrowed, updatedCopy!.Status);

            var borrowing = await context.Borrowings.FirstOrDefaultAsync(b => b.UserId == student.Id);
            Assert.NotNull(borrowing);
            Assert.Equal(BorrowingStatus.Active, borrowing!.Status);
            Assert.Equal(ReservationStatus.Fulfilled, reservation.Status);
        }

        [Fact]
        public async Task ReturnBookAsync_IncludesBookTitleInNotification()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            copy.Status = BookCopyStatus.Borrowed;
            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                BookCopy = copy,
                IssuedAt = DateTime.UtcNow.AddDays(-7),
                DueDate = DateTime.UtcNow.AddDays(7),
                Status = BorrowingStatus.Active
            };
            context.Borrowings.Add(borrowing);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var result = await borrowingService.ReturnBookAsync(borrowing.Id);

            Assert.True(result.Success);
            var studentNotifications = await notificationService.GetMyNotificationsAsync(student.Id);
            var returnNote = studentNotifications.FirstOrDefault(n => n.Type == NotificationType.BookReturned);
            Assert.NotNull(returnNote);
            // Verify notification does NOT say "the book", but actual title
            Assert.Contains("The Pragmatic Programmer", returnNote!.Message);
            Assert.DoesNotContain("Book 'the book'", returnNote.Message);
        }

        [Fact]
        public async Task ReturnBookAsync_Overdue_CalculatesFine()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            copy.Status = BookCopyStatus.Borrowed;
            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                BookCopy = copy,
                IssuedAt = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-3), // 3 days overdue
                Status = BorrowingStatus.Active
            };
            context.Borrowings.Add(borrowing);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var result = await borrowingService.ReturnBookAsync(borrowing.Id);

            Assert.True(result.Success);
            Assert.Contains("overdue by", result.Message);

            var fine = await context.Fines.FirstOrDefaultAsync(f => f.BorrowingId == borrowing.Id);
            Assert.NotNull(fine);
            Assert.Equal(3, fine!.OverdueDays);
            Assert.Equal(15.00m, fine.Amount); // 3 * ₹5.00
            Assert.Equal(FineStatus.Unpaid, fine.Status);
        }

        [Fact]
        public async Task SendOverdueRemindersAsync_SendsReminderOnlyOncePerDay()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            copy.Status = BookCopyStatus.Borrowed;
            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                BookCopy = copy,
                IssuedAt = DateTime.UtcNow.AddDays(-15),
                DueDate = DateTime.UtcNow.AddDays(-1), // 1 day overdue
                Status = BorrowingStatus.Active
            };
            context.Borrowings.Add(borrowing);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            int sent1 = await borrowingService.SendOverdueRemindersAsync();
            Assert.Equal(1, sent1);

            // Second consecutive run should not send again because of 24h throttling
            int sent2 = await borrowingService.SendOverdueRemindersAsync();
            Assert.Equal(0, sent2);

            var notes = await notificationService.GetMyNotificationsAsync(student.Id);
            Assert.Single(notes, n => n.Type == NotificationType.OverdueReminder);
        }
        [Fact]
        public async Task ReturnBookAsync_NonExistentBorrowing_ReturnsFail()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var result = await borrowingService.ReturnBookAsync(9999);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReturnBookAsync_AlreadyReturned_ReturnsFail()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopy = copy,
                Status = BorrowingStatus.Returned,
                IssuedAt = DateTime.UtcNow.AddDays(-10),
                DueDate = DateTime.UtcNow.AddDays(4),
                ReturnedAt = DateTime.UtcNow.AddDays(-1)
            };
            context.Borrowings.Add(borrowing);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var result = await borrowingService.ReturnBookAsync(borrowing.Id);

            Assert.False(result.Success);
            Assert.Contains("not active or has already been returned", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetMyBorrowingsAsync_CalculatesIsOverdueFlagCorrectly()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            var copy2 = new BookCopy { Book = book, CopyCode = "PRAG-002", Status = BookCopyStatus.Borrowed };
            context.BookCopies.Add(copy2);

            // 1. Overdue
            context.Borrowings.Add(new Borrowing
            {
                UserId = student.Id,
                BookCopy = copy,
                Status = BorrowingStatus.Active,
                IssuedAt = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-2)
            });

            // 2. Active on-time
            context.Borrowings.Add(new Borrowing
            {
                UserId = student.Id,
                BookCopy = copy2,
                Status = BorrowingStatus.Active,
                IssuedAt = DateTime.UtcNow.AddDays(-5),
                DueDate = DateTime.UtcNow.AddDays(9)
            });
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var fineService = new FineService(context, notificationService);
            var borrowingService = new BorrowingService(context, bookCopyService, waitlistService, fineService, notificationService);

            var borrowings = (await borrowingService.GetMyBorrowingsAsync(student.Id)).ToList();

            Assert.Equal(2, borrowings.Count);
            Assert.Contains(borrowings, b => b.IsOverdue);
            Assert.Contains(borrowings, b => !b.IsOverdue);
        }
    }
}