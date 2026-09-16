using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;

namespace LibraryReservationEngine.Infrastructure.Services
{
    // Minimal stub — full implementation lands in feature/waitlist.
    public class WaitlistService : IWaitlistService
    {
        public Task<Result> JoinWaitlistAsync(string userId, int bookId)
        {
            return Task.FromResult(Result.Ok("Added to waitlist (stub)."));
        }

        public Task<Result> LeaveWaitlistAsync(int waitlistEntryId, string userId)
        {
            return Task.FromResult(Result.Ok());
        }

        public Task PromoteNextInLineAsync(int bookId)
        {
            return Task.CompletedTask;
        }
    }
}