using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class FineServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(Book book, BookCopy copy, ApplicationUser student)> SeedDataAsync(ApplicationDbContext context)
        {
            var student = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "reader@test.com",
                Email = "reader@test.com",
                FullName = "Bob Smith"
            };
            var author = new Author { Name = "Martin Fowler" };
            var category = new Category { Name = "Architecture" };
            var book = new Book
            {
                Title = "Refactoring",
                ISBN = "978-0201485677",
                Author = author,
                Category = category
            };
            var copy = new BookCopy
            {
                Book = book,
                CopyCode = "REF-001",
                Status = BookCopyStatus.Borrowed
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
        public async Task CalculateFineAsync_OnTimeReturn_ReturnsZero()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedDataAsync(context);

            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                BookCopy = copy,
                IssuedAt = DateTime.UtcNow.AddDays(-10),
                DueDate = DateTime.UtcNow.AddDays(4),
                ReturnedAt = DateTime.UtcNow, // returned on time
                Status = BorrowingStatus.Returned
            };
            context.Borrowings.Add(borrowing);
            await context.SaveChangesAsync();

            var notificationService = new NotificationService(context);
            var fineService = new FineService(context, notificationService);

            var fineAmount = await fineService.CalculateFineAsync(borrowing.Id);

            Assert.Equal(0m, fineAmount);
            Assert.Empty(await context.Fines.ToListAsync());
        }

        [Fact]
        public async Task CalculateFineAsync_Overdue_CalculatesCorrectDailyRate()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedDataAsync(context);

            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                BookCopy = copy,
                IssuedAt = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-5), // 5 days late
                ReturnedAt = DateTime.UtcNow,
                Status = BorrowingStatus.Returned
            };
            context.Borrowings.Add(borrowing);
            await context.SaveChangesAsync();

            var notificationService = new NotificationService(context);
            var fineService = new FineService(context, notificationService);

            var fineAmount = await fineService.CalculateFineAsync(borrowing.Id);

            // 5 days * ₹5.00 = ₹25.00
            Assert.Equal(25.00m, fineAmount);

            var fine = await context.Fines.FirstOrDefaultAsync(f => f.BorrowingId == borrowing.Id);
            Assert.NotNull(fine);
            Assert.Equal(5, fine!.OverdueDays);
            Assert.Equal(25.00m, fine.Amount);
            Assert.Equal(FineStatus.Unpaid, fine.Status);
        }

        [Fact]
        public async Task PayFineAsync_StudentPaysOwnFine_MarksFineAsPaid()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedDataAsync(context);

            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                BookCopy = copy,
                IssuedAt = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-2),
                ReturnedAt = DateTime.UtcNow,
                Status = BorrowingStatus.Returned
            };
            context.Borrowings.Add(borrowing);

            var fine = new Fine
            {
                Borrowing = borrowing,
                Amount = 10.00m,
                OverdueDays = 2,
                Status = FineStatus.Unpaid,
                CreatedAt = DateTime.UtcNow
            };
            context.Fines.Add(fine);
            await context.SaveChangesAsync();

            var notificationService = new NotificationService(context);
            var fineService = new FineService(context, notificationService);

            var result = await fineService.PayFineAsync(fine.Id, student.Id);

            Assert.True(result.Success);
            var updatedFine = await context.Fines.FindAsync(fine.Id);
            Assert.Equal(FineStatus.Paid, updatedFine!.Status);
            // Paid verified by Status
        }

        [Fact]
        public async Task PayFineAsync_DifferentStudent_FailsAuthorization()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedDataAsync(context);

            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                Status = BorrowingStatus.Returned,
                IssuedAt = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-2)
            };
            context.Borrowings.Add(borrowing);

            var fine = new Fine
            {
                Borrowing = borrowing,
                Amount = 10.00m,
                OverdueDays = 2,
                Status = FineStatus.Unpaid,
                CreatedAt = DateTime.UtcNow
            };
            context.Fines.Add(fine);
            await context.SaveChangesAsync();

            var notificationService = new NotificationService(context);
            var fineService = new FineService(context, notificationService);

            var result = await fineService.PayFineAsync(fine.Id, "someone-else-id");

            Assert.False(result.Success);
            Assert.Contains("not belong to you", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task WaiveFineAsync_LibrarianWaivesFine_MarksFineAsWaived()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedDataAsync(context);

            var borrowing = new Borrowing
            {
                UserId = student.Id,
                BookCopyId = copy.Id,
                Status = BorrowingStatus.Returned,
                IssuedAt = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-2)
            };
            context.Borrowings.Add(borrowing);

            var fine = new Fine
            {
                Borrowing = borrowing,
                Amount = 10.00m,
                OverdueDays = 2,
                Status = FineStatus.Unpaid,
                CreatedAt = DateTime.UtcNow
            };
            context.Fines.Add(fine);
            await context.SaveChangesAsync();

            var notificationService = new NotificationService(context);
            var fineService = new FineService(context, notificationService);

            var result = await fineService.WaiveFineAsync(fine.Id);

            Assert.True(result.Success);
            var updatedFine = await context.Fines.FindAsync(fine.Id);
            Assert.Equal(FineStatus.Waived, updatedFine!.Status);
        }
    }
}
