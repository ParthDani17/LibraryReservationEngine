using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person A.
    public interface IReservationService
    {
        Task<Result> CreateReservationAsync(string userId, int bookId);
        Task<Result> CancelReservationAsync(int reservationId, string userId);
        Task<IEnumerable<object>> GetMyReservationsAsync(string userId);
    }
}
