using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class WaitlistServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(Book book, BookCopy copy)> SeedBookAsync(ApplicationDbContext context)
        {
            var author = new Author { Name = "Joshua Bloch" };
            var category = new Category { Name = "Programming" };
            var book = new Book
            {
                Title = "Effective Java",
                ISBN = "978-0134685991",
                Author = author,
                Category = category
            };
            var copy = new BookCopy
            {
                Book = book,
                CopyCode = "EJ-001",
                Status = BookCopyStatus.Borrowed
            };

            context.Authors.Add(author);
            context.Categories.Add(category);
            context.Books.Add(book);
            context.BookCopies.Add(copy);
            await context.SaveChangesAsync();

            return (book, copy);
        }

        [Fact]
        public async Task JoinWaitlistAsync_FIFOOrder_AssignsSequentialPositions()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, _) = await SeedBookAsync(context);

            var student1 = new ApplicationUser { Id = "s1", UserName = "s1@test.com" };
            var student2 = new ApplicationUser { Id = "s2", UserName = "s2@test.com" };
            var student3 = new ApplicationUser { Id = "s3", UserName = "s3@test.com" };
            context.Users.AddRange(student1, student2, student3);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);

            var res1 = await waitlistService.JoinWaitlistAsync(student1.Id, book.Id);
            var res2 = await waitlistService.JoinWaitlistAsync(student2.Id, book.Id);
            var res3 = await waitlistService.JoinWaitlistAsync(student3.Id, book.Id);

            Assert.True(res1.Success);
            Assert.True(res2.Success);
            Assert.True(res3.Success);

            var entries = await context.WaitlistEntries
                .Where(w => w.BookId == book.Id && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.Position)
                .ToListAsync();

            Assert.Equal(3, entries.Count);
            Assert.Equal(1, entries[0].Position);
            Assert.Equal(student1.Id, entries[0].UserId);
            Assert.Equal(2, entries[1].Position);
            Assert.Equal(student2.Id, entries[1].UserId);
            Assert.Equal(3, entries[2].Position);
            Assert.Equal(student3.Id, entries[2].UserId);
        }

        [Fact]
        public async Task JoinWaitlistAsync_DuplicateEntry_Blocked()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, _) = await SeedBookAsync(context);

            var student = new ApplicationUser { Id = "student-x", UserName = "sx@test.com" };
            context.Users.Add(student);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);

            var first = await waitlistService.JoinWaitlistAsync(student.Id, book.Id);
            Assert.True(first.Success);

            var duplicate = await waitlistService.JoinWaitlistAsync(student.Id, book.Id);
            Assert.False(duplicate.Success);
            Assert.Contains("already on the waitlist", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LeaveWaitlistAsync_ReordersPositionsAfterLeaving()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, _) = await SeedBookAsync(context);

            var student1 = new ApplicationUser { Id = "s1", UserName = "s1@test.com" };
            var student2 = new ApplicationUser { Id = "s2", UserName = "s2@test.com" };
            var student3 = new ApplicationUser { Id = "s3", UserName = "s3@test.com" };
            context.Users.AddRange(student1, student2, student3);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);

            await waitlistService.JoinWaitlistAsync(student1.Id, book.Id); // Position 1
            await waitlistService.JoinWaitlistAsync(student2.Id, book.Id); // Position 2
            await waitlistService.JoinWaitlistAsync(student3.Id, book.Id); // Position 3

            var entry1 = await context.WaitlistEntries.FirstAsync(w => w.UserId == student1.Id);

            // Student 1 leaves the waitlist
            var leaveResult = await waitlistService.LeaveWaitlistAsync(entry1.Id, student1.Id);
            Assert.True(leaveResult.Success);

            // Remaining entries must now be shifted up: student2 -> Position 1, student3 -> Position 2
            var remaining = await context.WaitlistEntries
                .Where(w => w.BookId == book.Id && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.Position)
                .ToListAsync();

            Assert.Equal(2, remaining.Count);
            Assert.Equal(student2.Id, remaining[0].UserId);
            Assert.Equal(1, remaining[0].Position);
            Assert.Equal(student3.Id, remaining[1].UserId);
            Assert.Equal(2, remaining[1].Position);
        }

        [Fact]
        public async Task PromoteNextInLineAsync_PromotesFirstUserInFIFOQueue()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var (book, copy) = await SeedBookAsync(context);

            var student1 = new ApplicationUser { Id = "s1", UserName = "s1@test.com" };
            var student2 = new ApplicationUser { Id = "s2", UserName = "s2@test.com" };
            context.Users.AddRange(student1, student2);
            await context.SaveChangesAsync();

            var bookCopyService = new BookCopyService(context);
            var notificationService = new NotificationService(context);
            var waitlistService = new WaitlistService(context, bookCopyService, notificationService);

            await waitlistService.JoinWaitlistAsync(student1.Id, book.Id); // Position 1
            await waitlistService.JoinWaitlistAsync(student2.Id, book.Id); // Position 2

            // Make the copy Available
            copy.Status = BookCopyStatus.Available;
            await context.SaveChangesAsync();

            // Trigger promotion
            await waitlistService.PromoteNextInLineAsync(book.Id);

            // student1 should be promoted
            var entry1 = await context.WaitlistEntries.FirstAsync(w => w.UserId == student1.Id);
            Assert.Equal(WaitlistStatus.Promoted, entry1.Status);

            var res1 = await context.Reservations.FirstOrDefaultAsync(r => r.UserId == student1.Id);
            Assert.NotNull(res1);
            Assert.Equal(ReservationStatus.Active, res1!.Status);
            Assert.Equal(copy.Id, res1.BookCopyId);

            // student2 remains waiting and shifted to Position 1
            var entry2 = await context.WaitlistEntries.FirstAsync(w => w.UserId == student2.Id);
            Assert.Equal(WaitlistStatus.Waiting, entry2.Status);
            Assert.Equal(1, entry2.Position);
        }
    }
}

