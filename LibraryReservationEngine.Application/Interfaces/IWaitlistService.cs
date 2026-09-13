using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person A.
    public interface IWaitlistService
    {
        Task<Result> JoinWaitlistAsync(string userId, int bookId);
        Task<Result> LeaveWaitlistAsync(int waitlistEntryId, string userId);
        Task PromoteNextInLineAsync(int bookId);
    }
}
