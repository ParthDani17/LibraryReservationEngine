using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person B.
    public interface IFineService
    {
        Task<decimal> CalculateFineAsync(int borrowingId);
        Task<Result> MarkFineAsPaidAsync(int fineId);
        Task<Result> WaiveFineAsync(int fineId);
        Task<IEnumerable<FineSummaryDto>> GetMyFinesAsync(string userId);
        Task<IEnumerable<FineSummaryDto>> GetAllFinesAsync();
    }
}
