using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person B.
    public interface IFineService
    {
        Task<decimal> CalculateFineAsync(int borrowingId);
        Task<Result> MarkFineAsPaidAsync(int fineId);
    }
}
