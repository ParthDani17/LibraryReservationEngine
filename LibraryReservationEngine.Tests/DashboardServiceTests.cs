using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class DashboardServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetStatsAsync_CalculatesOverdueBorrowingsAsDisplayCondition()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);

            var student = new ApplicationUser { Id = "s-dash", UserName = "dash@test.com" };
            context.Users.Add(student);

            var author = new Author { Name = "Kent Beck" };
            var category = new Category { Name = "Agile" };
            var book = new Book { Title = "TDD By Example", ISBN = "978-0321146533", Author = author, Category = category };
            var copy1 = new BookCopy { Book = book, CopyCode = "TDD-001", Status = BookCopyStatus.Borrowed };
            var copy2 = new BookCopy { Book = book, CopyCode = "TDD-002", Status = BookCopyStatus.Borrowed };
            var copy3 = new BookCopy { Book = book, CopyCode = "TDD-003", Status = BookCopyStatus.Available };
            context.Books.Add(book);
            context.BookCopies.AddRange(copy1, copy2, copy3);

            // 1. Overdue active borrowing (Due 2 days ago)
            context.Borrowings.Add(new Borrowing
            {
                UserId = student.Id,
                BookCopy = copy1,
                IssuedAt = DateTime.UtcNow.AddDays(-16),
                DueDate = DateTime.UtcNow.AddDays(-2),
                Status = BorrowingStatus.Active
            });

            // 2. On-time active borrowing (Due in 5 days)
            context.Borrowings.Add(new Borrowing
            {
                UserId = student.Id,
                BookCopy = copy2,
                IssuedAt = DateTime.UtcNow.AddDays(-5),
                DueDate = DateTime.UtcNow.AddDays(9),
                Status = BorrowingStatus.Active
            });

            // 3. Past-due but already returned (Should NOT count as overdue)
            context.Borrowings.Add(new Borrowing
            {
                UserId = student.Id,
                BookCopy = copy3,
                IssuedAt = DateTime.UtcNow.AddDays(-30),
                DueDate = DateTime.UtcNow.AddDays(-16),
                ReturnedAt = DateTime.UtcNow.AddDays(-10),
                Status = BorrowingStatus.Returned
            });

            await context.SaveChangesAsync();

            var service = new DashboardService(context);
            var stats = await service.GetStatsAsync();

            // Verify only the 1 active overdue borrowing is counted
            Assert.Equal(1, stats.OverdueBorrowings);
            Assert.Equal(2, stats.BorrowedCopies);
            Assert.Equal(1, stats.AvailableCopies);
        }
    }
}

