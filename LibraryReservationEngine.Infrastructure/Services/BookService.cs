using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class BookService : IBookService
    {
        private readonly ApplicationDbContext _context;

        public BookService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Book> CreateBookAsync(string title, string isbn, string authorName, string categoryName)
        {
            var author = await FindOrCreateAuthorAsync(authorName);
            var category = await FindOrCreateCategoryAsync(categoryName);

            var book = new Book
            {
                Title = title,
                ISBN = isbn,
                AuthorId = author.Id,
                CategoryId = category.Id
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return book;
        }

        public async Task<Result> UpdateBookAsync(int bookId, string title, string isbn, string authorName, string categoryName)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book is null) return Result.Fail("Book not found");

            var author = await FindOrCreateAuthorAsync(authorName);
            var category = await FindOrCreateCategoryAsync(categoryName);

            book.Title = title;
            book.ISBN = isbn;
            book.AuthorId = author.Id;
            book.CategoryId = category.Id;

            await _context.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result> DeleteBookAsync(int bookId)
        {
            var book = await _context.Books
                .Include(b => b.BookCopies)
                .FirstOrDefaultAsync(b => b.Id == bookId);

            if (book is null) return Result.Fail("Book not found");

            bool hasActiveCopies = book.BookCopies.Any(c => c.Status != Domain.Enums.BookCopyStatus.Available);
            if (hasActiveCopies)
                return Result.Fail("Cannot delete: some copies are reserved or borrowed");

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<IEnumerable<Book>> SearchBooksAsync(string? query)
        {
            var books = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Include(b => b.BookCopies)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                books = books.Where(b =>
                    b.Title.Contains(query) ||
                    b.Author!.Name.Contains(query) ||
                    b.Category!.Name.Contains(query));
            }

            return await books.ToListAsync();
        }

        public async Task<Book?> GetBookDetailsAsync(int bookId)
        {
            return await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Include(b => b.BookCopies)
                .FirstOrDefaultAsync(b => b.Id == bookId);
        }

        private async Task<Author> FindOrCreateAuthorAsync(string name)
        {
            var author = await _context.Authors.FirstOrDefaultAsync(a => a.Name == name);
            if (author is not null) return author;

            author = new Author { Name = name };
            _context.Authors.Add(author);
            await _context.SaveChangesAsync();
            return author;
        }

        private async Task<Category> FindOrCreateCategoryAsync(string name)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == name);
            if (category is not null) return category;

            category = new Category { Name = name };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }
    }
}