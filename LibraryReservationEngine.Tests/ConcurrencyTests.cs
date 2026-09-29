using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class ConcurrencyTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task TwoUsersReservingLastCopy_OnlyOneSucceeds()
        {
            // Arrange — a shared in-memory database (same name = same data for both "requests")
            var dbName = Guid.NewGuid().ToString();

            using (var seedContext = CreateContext(dbName))
            {
                var author = new Author { Name = "Robert C. Martin" };
                var category = new Category { Name = "Programming" };
                var book = new Book { Title = "Clean Code", ISBN = "123", Author = author, Category = category };
                var copy = new BookCopy { Book = book, CopyCode = "CLN-001", Status = BookCopyStatus.Available };

                seedContext.Authors.Add(author);
                seedContext.Categories.Add(category);
                seedContext.Books.Add(book);
                seedContext.BookCopies.Add(copy);
                await seedContext.SaveChangesAsync();
            }

            // Act — simulate two separate requests (two separate DbContext instances,
            // like two separate HTTP requests would each get in real ASP.NET Core),
            // both trying to claim the same copy at the same time.
            using var contextA = CreateContext(dbName);
            using var contextB = CreateContext(dbName);

            var serviceA = new BookCopyService(contextA);
            var serviceB = new BookCopyService(contextB);

            var bookId = await contextA.Books.Select(b => b.Id).FirstAsync();
            var copyId = (await serviceA.FindAvailableCopyIdAsync(bookId))!.Value;

            // Both "requests" loaded the copy as Available before either one commits —
            // this mirrors the real race condition.
            var taskA = serviceA.MarkAsReservedAsync(copyId);
            var taskB = serviceB.MarkAsReservedAsync(copyId);

            var results = await Task.WhenAll(taskA, taskB);

            // Assert — exactly one of the two succeeded, not both, not neither.
            int successCount = results.Count(r => r == true);
            Assert.Equal(1, successCount);

            // Confirm the database itself agrees: the copy is Reserved, not double-booked.
            using var verifyContext = CreateContext(dbName);
            var finalCopy = await verifyContext.BookCopies.FindAsync(copyId);
            Assert.Equal(BookCopyStatus.Reserved, finalCopy!.Status);
        }
    }
}