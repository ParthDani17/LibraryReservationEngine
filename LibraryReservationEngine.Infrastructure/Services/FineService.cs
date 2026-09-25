using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class FineService : IFineService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        private const decimal DailyOverdueRate = 5.00m; // ₹5.00 per day

        public FineService(
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<decimal> CalculateFineAsync(int borrowingId)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.BookCopy)
                    .ThenInclude(c => c!.Book)
                .Include(b => b.Fine)
                .FirstOrDefaultAsync(b => b.Id == borrowingId);

            if (borrowing is null)
            {
                return 0m;
            }

            var returnDate = borrowing.ReturnedAt ?? DateTime.UtcNow;

            if (returnDate <= borrowing.DueDate)
            {
                return 0m;
            }

            int overdueDays = (int)Math.Ceiling((returnDate.Date - borrowing.DueDate.Date).TotalDays);
            if (overdueDays <= 0)
            {
                return 0m;
            }

            decimal fineAmount = overdueDays * DailyOverdueRate;

            // If a fine record already exists for this borrowing, update it if unpaid
            if (borrowing.Fine != null)
            {
                if (borrowing.Fine.Status == FineStatus.Unpaid)
                {
                    borrowing.Fine.Amount = fineAmount;
                    borrowing.Fine.OverdueDays = overdueDays;
                    await _context.SaveChangesAsync();
                }
                return borrowing.Fine.Amount;
            }

            // Create new fine record
            var fine = new Fine
            {
                BorrowingId = borrowingId,
                Amount = fineAmount,
                OverdueDays = overdueDays,
                Status = FineStatus.Unpaid,
                CreatedAt = DateTime.UtcNow
            };

            _context.Fines.Add(fine);
            await _context.SaveChangesAsync();

            // Dispatch notification to user
            string bookTitle = borrowing.BookCopy?.Book?.Title ?? "your borrowed book";
            await _notificationService.SendAsync(
                borrowing.UserId,
                NotificationType.FineIssued,
                $"You have an overdue fine of ₹{fineAmount:F2} for {overdueDays} day(s) on '{bookTitle}'. Please settle it with the library.");

            return fineAmount;
        }

        public async Task<Result> MarkFineAsPaidAsync(int fineId)
        {
            var fine = await _context.Fines.FindAsync(fineId);

            if (fine is null)
            {
                return Result.Fail("Fine record not found.");
            }

            if (fine.Status == FineStatus.Paid)
            {
                return Result.Fail("This fine is already marked as paid.");
            }

            if (fine.Status == FineStatus.Waived)
            {
                return Result.Fail("This fine has been waived and cannot be paid.");
            }

            fine.Status = FineStatus.Paid;
            await _context.SaveChangesAsync();

            return Result.Ok("Fine marked as paid successfully.");
        }

        public async Task<Result> WaiveFineAsync(int fineId)
        {
            var fine = await _context.Fines.FindAsync(fineId);

            if (fine is null)
            {
                return Result.Fail("Fine record not found.");
            }

            if (fine.Status == FineStatus.Waived)
            {
                return Result.Fail("This fine is already waived.");
            }

            if (fine.Status == FineStatus.Paid)
            {
                return Result.Fail("Cannot waive an already paid fine.");
            }

            fine.Status = FineStatus.Waived;
            await _context.SaveChangesAsync();

            return Result.Ok("Fine waived successfully.");
        }

        public async Task<IEnumerable<FineSummaryDto>> GetMyFinesAsync(string userId)
        {
            return await _context.Fines
                .AsNoTracking()
                .Where(f => f.Borrowing != null && f.Borrowing.UserId == userId)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b!.User)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b!.BookCopy)
                        .ThenInclude(c => c!.Book)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FineSummaryDto
                {
                    Id = f.Id,
                    BorrowingId = f.BorrowingId,
                    MemberId = f.Borrowing != null ? f.Borrowing.UserId : "",
                    MemberName = f.Borrowing != null && f.Borrowing.User != null ? f.Borrowing.User.FullName : "Unknown",
                    MemberEmail = f.Borrowing != null && f.Borrowing.User != null ? f.Borrowing.User.Email ?? "" : "",
                    BookTitle = f.Borrowing != null && f.Borrowing.BookCopy != null && f.Borrowing.BookCopy.Book != null
                        ? f.Borrowing.BookCopy.Book.Title
                        : "Unknown Book",
                    CopyCode = f.Borrowing != null && f.Borrowing.BookCopy != null ? f.Borrowing.BookCopy.CopyCode : "-",
                    Amount = f.Amount,
                    OverdueDays = f.OverdueDays,
                    Status = f.Status.ToString(),
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<FineSummaryDto>> GetAllFinesAsync()
        {
            return await _context.Fines
                .AsNoTracking()
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b!.User)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b!.BookCopy)
                        .ThenInclude(c => c!.Book)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FineSummaryDto
                {
                    Id = f.Id,
                    BorrowingId = f.BorrowingId,
                    MemberId = f.Borrowing != null ? f.Borrowing.UserId : "",
                    MemberName = f.Borrowing != null && f.Borrowing.User != null ? f.Borrowing.User.FullName : "Unknown",
                    MemberEmail = f.Borrowing != null && f.Borrowing.User != null ? f.Borrowing.User.Email ?? "" : "",
                    BookTitle = f.Borrowing != null && f.Borrowing.BookCopy != null && f.Borrowing.BookCopy.Book != null
                        ? f.Borrowing.BookCopy.Book.Title
                        : "Unknown Book",
                    CopyCode = f.Borrowing != null && f.Borrowing.BookCopy != null ? f.Borrowing.BookCopy.CopyCode : "-",
                    Amount = f.Amount,
                    OverdueDays = f.OverdueDays,
                    Status = f.Status.ToString(),
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();
        }
    }
}
