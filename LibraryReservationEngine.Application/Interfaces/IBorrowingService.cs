using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person B. Depends on IBookCopyService (Person A's contract),
    // not on Person A's actual code.
    public interface IBorrowingService
    {
        Task<Result> IssueBookAsync(int reservationId);
        Task<IEnumerable<BorrowingSummaryDto>> GetMyBorrowingsAsync(string userId);
    }
}
