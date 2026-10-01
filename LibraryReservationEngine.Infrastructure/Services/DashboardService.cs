using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> GetStatsAsync()
        {
            var stats = new DashboardStatsDto
            {
                TotalBooks = await _context.Books.CountAsync(),
                TotalCopies = await _context.BookCopies.CountAsync(),

                AvailableCopies = await _context.BookCopies
                    .CountAsync(c => c.Status == BookCopyStatus.Available),
                BorrowedCopies = await _context.BookCopies
                    .CountAsync(c => c.Status == BookCopyStatus.Borrowed),
                ReservedCopies = await _context.BookCopies
                    .CountAsync(c => c.Status == BookCopyStatus.Reserved),
                MaintenanceCopies = await _context.BookCopies
                    .CountAsync(c => c.Status == BookCopyStatus.Maintenance),

                ActiveReservations = await _context.Reservations
                    .CountAsync(r => r.Status == ReservationStatus.Active),
                ActiveWaitlistEntries = await _context.WaitlistEntries
                    .CountAsync(w => w.Status == WaitlistStatus.Waiting),

                OverdueBorrowings = await _context.Borrowings
                    .CountAsync(b => b.Status == BorrowingStatus.Overdue),

                TotalOutstandingFines = await _context.Fines
                    .Where(f => f.Status == FineStatus.Unpaid)
                    .Select(f => (decimal?)f.Amount)
                    .SumAsync() ?? 0m,

                TotalRegisteredMembers = await _context.Users.CountAsync()
            };

            return stats;
        }
    }
}