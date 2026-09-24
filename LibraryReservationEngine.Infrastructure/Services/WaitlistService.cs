using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class WaitlistService : IWaitlistService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookCopyService _bookCopyService;
        private readonly INotificationService _notificationService;

        private const int HoldPeriodHours = 24;

        public WaitlistService(
            ApplicationDbContext context,
            IBookCopyService bookCopyService,
            INotificationService notificationService)
        {
            _context = context;
            _bookCopyService = bookCopyService;
            _notificationService = notificationService;
        }

        public async Task<Result> JoinWaitlistAsync(string userId, int bookId)
        {
            bool already = await _context.WaitlistEntries.AnyAsync(w =>
                w.UserId == userId &&
                w.BookId == bookId &&
                w.Status == WaitlistStatus.Waiting);

            if (already)
                return Result.Fail("You're already on the waitlist for this book.");

            int lastPosition = await _context.WaitlistEntries
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .Select(w => (int?)w.Position)
                .MaxAsync() ?? 0;

            var entry = new WaitlistEntry
            {
                UserId = userId,
                BookId = bookId,
                Position = lastPosition + 1,
                Status = WaitlistStatus.Waiting,
                JoinedAt = DateTime.UtcNow
            };

            _context.WaitlistEntries.Add(entry);
            await _context.SaveChangesAsync();

            return Result.Ok($"No copies available — you're #{entry.Position} on the waitlist.");
        }

        public async Task<Result> LeaveWaitlistAsync(int waitlistEntryId, string userId)
        {
            var entry = await _context.WaitlistEntries.FindAsync(waitlistEntryId);
            if (entry is null) return Result.Fail("Waitlist entry not found.");
            if (entry.UserId != userId) return Result.Fail("This isn't your waitlist entry.");
            if (entry.Status != WaitlistStatus.Waiting) return Result.Fail("This entry is no longer active.");

            entry.Status = WaitlistStatus.Cancelled;
            await _context.SaveChangesAsync();

            await ReorderPositionsAfterAsync(entry.BookId, entry.Position);

            return Result.Ok("Removed from waitlist.");
        }

        public async Task PromoteNextInLineAsync(int bookId)
        {
            var next = await _context.WaitlistEntries
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.Position)
                .FirstOrDefaultAsync();

            if (next is null) return; // nobody waiting — copy just stays Available

            var copyId = await _bookCopyService.FindAvailableCopyIdAsync(bookId);
            if (copyId is null) return; // shouldn't normally happen, but guard anyway

            var claimed = await _bookCopyService.MarkAsReservedAsync(copyId.Value);
            if (!claimed) return; // lost a race — leave them on the waitlist, will retry next promotion trigger

            var reservation = new Reservation
            {
                UserId = next.UserId,
                BookId = bookId,
                BookCopyId = copyId.Value,
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(HoldPeriodHours)
            };
            _context.Reservations.Add(reservation);

            next.Status = WaitlistStatus.Promoted;
            await _context.SaveChangesAsync();

            await ReorderPositionsAfterAsync(bookId, next.Position);

            await _notificationService.SendAsync(
                next.UserId, NotificationType.WaitlistPromoted,
                "A copy is now reserved for you — you were promoted from the waitlist.");
        }

        private async Task ReorderPositionsAfterAsync(int bookId, int removedPosition)
        {
            var toShift = await _context.WaitlistEntries
                .Where(w => w.BookId == bookId &&
                            w.Status == WaitlistStatus.Waiting &&
                            w.Position > removedPosition)
                .ToListAsync();

            foreach (var entry in toShift)
                entry.Position -= 1;

            if (toShift.Count > 0)
                await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<WaitlistSummaryDto>> GetMyWaitlistEntriesAsync(string userId)
        {
            return await _context.WaitlistEntries
                .Where(w => w.UserId == userId && w.Status == WaitlistStatus.Waiting)
                .Include(w => w.Book)
                .OrderBy(w => w.Position)
                .Select(w => new WaitlistSummaryDto
                {
                    Id = w.Id,
                    BookTitle = w.Book!.Title,
                    Position = w.Position,
                    Status = w.Status.ToString(),
                    JoinedAt = w.JoinedAt
                })
                .ToListAsync();
        }
    }
}