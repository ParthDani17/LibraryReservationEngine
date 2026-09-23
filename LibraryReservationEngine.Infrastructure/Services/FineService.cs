using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Infrastructure.Data;

namespace LibraryReservationEngine.Infrastructure.Services
{
    // Minimal stub — full implementation lands in feature/fines.
    public class FineService : IFineService
    {
        private readonly ApplicationDbContext _context;

        public FineService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<decimal> CalculateFineAsync(int borrowingId)
        {
            // Stub returning 0. Full calculation and fine creation implemented in feature/fines.
            return Task.FromResult(0m);
        }

        public Task<Result> MarkFineAsPaidAsync(int fineId)
        {
            // Stub returning Ok. Full payment logic implemented in feature/fines.
            return Task.FromResult(Result.Ok("Fine marked as paid (stub)."));
        }
    }
}
