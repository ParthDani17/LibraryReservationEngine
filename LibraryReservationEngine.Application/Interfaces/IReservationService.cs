using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    public interface IReservationService
    {
        Task<Result> CreateReservationAsync(string userId, int bookId);
        Task<Result> CancelReservationAsync(int reservationId, string userId);
        Task<IEnumerable<ReservationSummaryDto>> GetMyReservationsAsync(string userId);
    }
}