using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class ReservationServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(Book book, BookCopy copy, ApplicationUser student)> SeedBookAndStudentAsync(ApplicationDbContext context)
        {
            var student = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "charlie@test.com",
                Email = "charlie@test.com",
                FullName = "Charlie Brown"
            };
            var author = new Author { Name = "Eric Evans" };
            var category = new Category { Name = "Domain-Driven Design" };
            var book = new Book
            {
                Title = "Domain-Driven Design",
                ISBN = "978-0321125217",
                Author = author,
                Category = category
            };
            var copy = new BookCopy
            {
                Book = book,
                CopyCode = "DDD-001",
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
        public async Task CreateReservationAsync_AvailableCopy_CreatesActiveReservation()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            var result = await reservationService.CreateReservationAsync(student.Id, book.Id);

            Assert.True(result.Success);
            var reservation = await context.Reservations.FirstOrDefaultAsync(r => r.UserId == student.Id && r.BookId == book.Id);
            Assert.NotNull(reservation);
            Assert.Equal(ReservationStatus.Active, reservation!.Status);
            Assert.Equal(copy.Id, reservation.BookCopyId);
            Assert.NotNull(reservation.ExpiresAt);

            var updatedCopy = await context.BookCopies.FindAsync(copy.Id);
            Assert.Equal(BookCopyStatus.Reserved, updatedCopy!.Status);
        }

        [Fact]
        public async Task CreateReservationAsync_DuplicateReservation_Blocked()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            var firstResult = await reservationService.CreateReservationAsync(student.Id, book.Id);
            Assert.True(firstResult.Success);

            // Attempt duplicate reservation
            var secondResult = await reservationService.CreateReservationAsync(student.Id, book.Id);
            Assert.False(secondResult.Success);
            Assert.Contains("already have a reservation", secondResult.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateReservationAsync_NoCopiesAvailable_AutomaticallyJoinsWaitlist()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            // Mark the only copy as Borrowed
            copy.Status = BookCopyStatus.Borrowed;
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            var result = await reservationService.CreateReservationAsync(student.Id, book.Id);

            Assert.True(result.Success);
            Assert.Contains("waitlist", result.Message, StringComparison.OrdinalIgnoreCase);

            var waitlistEntry = await context.WaitlistEntries.FirstOrDefaultAsync(w => w.UserId == student.Id && w.BookId == book.Id);
            Assert.NotNull(waitlistEntry);
            Assert.Equal(1, waitlistEntry!.Position);
            Assert.Equal(WaitlistStatus.Waiting, waitlistEntry.Status);
        }

        [Fact]
        public async Task ExpireOverdueReservationsAsync_MarksExpiredAndPromotesNextInWaitlist()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student1) = await SeedBookAndStudentAsync(context);

            var student2 = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "student2@test.com",
                Email = "student2@test.com",
                FullName = "Dana Scully"
            };
            context.Users.Add(student2);

            // student1 holds an expired reservation
            copy.Status = BookCopyStatus.Reserved;
            var expiredReservation = new Reservation
            {
                UserId = student1.Id,
                BookId = book.Id,
                BookCopyId = copy.Id,
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow.AddHours(-48),
                ExpiresAt = DateTime.UtcNow.AddHours(-1) // expired 1 hour ago
            };
            context.Reservations.Add(expiredReservation);

            // student2 is waiting on waitlist
            var waitlistEntry = new WaitlistEntry
            {
                UserId = student2.Id,
                BookId = book.Id,
                Position = 1,
                Status = WaitlistStatus.Waiting,
                JoinedAt = DateTime.UtcNow.AddHours(-10)
            };
            context.WaitlistEntries.Add(waitlistEntry);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            int expiredCount = await reservationService.ExpireOverdueReservationsAsync();

            Assert.Equal(1, expiredCount);

            // student1 reservation expired
            var updatedRes1 = await context.Reservations.FindAsync(expiredReservation.Id);
            Assert.Equal(ReservationStatus.Expired, updatedRes1!.Status);

            // student2 promoted from waitlist to active reservation
            var updatedWaitlist = await context.WaitlistEntries.FindAsync(waitlistEntry.Id);
            Assert.Equal(WaitlistStatus.Promoted, updatedWaitlist!.Status);

            var promotedRes = await context.Reservations.FirstOrDefaultAsync(r => r.UserId == student2.Id && r.BookId == book.Id);
            Assert.NotNull(promotedRes);
            Assert.Equal(ReservationStatus.Active, promotedRes!.Status);
            Assert.Equal(copy.Id, promotedRes.BookCopyId);
        }
        [Fact]
        public async Task CancelReservationAsync_ValidReservation_ReleasesCopyAndCancels()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            copy.Status = BookCopyStatus.Reserved;
            var res = new Reservation
            {
                UserId = student.Id,
                BookId = book.Id,
                BookCopyId = copy.Id,
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
            context.Reservations.Add(res);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            var cancelResult = await reservationService.CancelReservationAsync(res.Id, student.Id);

            Assert.True(cancelResult.Success);
            var updatedRes = await context.Reservations.FindAsync(res.Id);
            Assert.Equal(ReservationStatus.Cancelled, updatedRes!.Status);

            var updatedCopy = await context.BookCopies.FindAsync(copy.Id);
            Assert.Equal(BookCopyStatus.Available, updatedCopy!.Status);
        }

        [Fact]
        public async Task CancelReservationAsync_WrongUser_ReturnsFail()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            var res = new Reservation
            {
                UserId = student.Id,
                BookId = book.Id,
                BookCopyId = copy.Id,
                Status = ReservationStatus.Active
            };
            context.Reservations.Add(res);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            var cancelResult = await reservationService.CancelReservationAsync(res.Id, "wrong-user");

            Assert.False(cancelResult.Success);
            Assert.Contains("isn't your reservation", cancelResult.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetMyReservationsAsync_ReturnsOnlyTargetUserReservations()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy, student) = await SeedBookAndStudentAsync(context);

            var otherUser = new ApplicationUser { Id = "other", UserName = "other@test.com" };
            context.Users.Add(otherUser);

            context.Reservations.Add(new Reservation { UserId = student.Id, Book = book, Status = ReservationStatus.Active });
            context.Reservations.Add(new Reservation { UserId = otherUser.Id, Book = book, Status = ReservationStatus.Active });
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);
            var reservationService = new ReservationService(context, bookCopyService, waitlistService, notificationService);

            var myReservations = (await reservationService.GetMyReservationsAsync(student.Id)).ToList();

            Assert.Single(myReservations);
            Assert.Equal(book.Title, myReservations[0].BookTitle);
        }
    }
}