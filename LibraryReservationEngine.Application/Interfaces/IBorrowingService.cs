using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person B. Depends on IBookCopyService (Person A's contract),
    // not on Person A's actual code.
    public interface IBorrowingService
    {
        Task<Result> IssueBookAsync(string userId, int reservationId);
        Task<Result> ReturnBookAsync(int borrowingId);
        Task<IEnumerable<object>> GetMyBorrowingsAsync(string userId);
    }
}
