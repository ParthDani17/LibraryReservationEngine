using System;
using System.Collections.Generic;
using System.Text;
using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Domain.Entities;

namespace LibraryReservationEngine.Application.Interfaces
{
    public interface IBookService
    {
        Task<Book> CreateBookAsync(string title, string isbn, string authorName, string categoryName);
        Task<Result> UpdateBookAsync(int bookId, string title, string isbn, string authorName, string categoryName);
        Task<Result> DeleteBookAsync(int bookId);
        Task<IEnumerable<Book>> SearchBooksAsync(string? query);
        Task<Book?> GetBookDetailsAsync(int bookId);
    }
}
