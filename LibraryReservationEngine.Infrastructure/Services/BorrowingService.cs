using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class BorrowingService : IBorrowingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookCopyService _bookCopyService;
        private readonly IWaitlistService _waitlistService;
        private readonly IFineService _fineService;

        public BorrowingService(
            ApplicationDbContext context,
            IBookCopyService bookCopyService,
            IWaitlistService waitlistService,
            IFineService fineService)
        {
            _context = context;
            _bookCopyService = bookCopyService;
            _waitlistService = waitlistService;
            _fineService = fineService;
        }

        public async Task<Result> IssueBookAsync(int reservationId)
        {
            // 1. Find the reservation
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation is null)
            {
                return Result.Fail("Reservation not found.");
            }

            // 2. Reservation must be Active
            if (reservation.Status != ReservationStatus.Active)
            {
                return Result.Fail("Reservation is not active.");
            }

            // 3. Check whether the reservation has expired
            if (reservation.ExpiresAt.HasValue && reservation.ExpiresAt.Value <= DateTime.UtcNow)
            {
                return Result.Fail("Reservation has expired.");
            }

            // 4. A reservation must have a specific book copy
            if (!reservation.BookCopyId.HasValue)
            {
                return Result.Fail("No book copy is assigned to this reservation.");
            }

            int copyId = reservation.BookCopyId.Value;

            // 5. Check whether this copy is already being borrowed
            bool borrowingExists = await _context.Borrowings
                .AnyAsync(b => b.BookCopyId == copyId && b.Status == BorrowingStatus.Active);

            if (borrowingExists)
            {
                return Result.Fail("This book copy is already borrowed.");
            }

            // 6. Check borrowing limit (maximum 3 active borrowings per member)
            const int maxActiveBorrowings = 3;

            int activeBorrowingCount = await _context.Borrowings
                .CountAsync(b => b.UserId == reservation.UserId && b.Status == BorrowingStatus.Active);

            if (activeBorrowingCount >= maxActiveBorrowings)
            {
                return Result.Fail("Member has reached the maximum borrowing limit.");
            }

            // 7. Atomic transaction boundary for:
            //    - Marking copy as Borrowed
            //    - Creating the Borrowing record
            //    - Fulfilling the Reservation
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update BookCopy status
                    bool copyMarkedBorrowed = await _bookCopyService.MarkAsBorrowedAsync(copyId);
                    if (!copyMarkedBorrowed)
                    {
                        await transaction.RollbackAsync();
                        return Result.Fail("Unable to mark the book copy as borrowed.");
                    }

                    // Create the borrowing record
                    var borrowing = new Borrowing
                    {
                        UserId = reservation.UserId,
                        BookCopyId = copyId,
                        IssuedAt = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(-3),
                        Status = BorrowingStatus.Active
                    };
                    _context.Borrowings.Add(borrowing);

                    // Fulfill the reservation
                    reservation.Status = ReservationStatus.Fulfilled;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Result.Ok("Book issued successfully.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    return Result.Fail("A concurrency conflict occurred while issuing the book. Please try again.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<Result> ReturnBookAsync(int borrowingId)
        {
            // 1. Find the borrowing record
            var borrowing = await _context.Borrowings
                .Include(b => b.BookCopy)
                .FirstOrDefaultAsync(b => b.Id == borrowingId);

            if (borrowing is null)
            {
                return Result.Fail("Borrowing record not found.");
            }

            // 2. The borrowing must be Active
            if (borrowing.Status != BorrowingStatus.Active)
            {
                return Result.Fail("This borrowing record is not active or has already been returned.");
            }

            int copyId = borrowing.BookCopyId;
            int bookId = borrowing.BookCopy?.BookId ?? 0;

            // 3. Atomic execution for copy release and borrowing return
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            var result = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Mark copy as Available
                    bool copyMarkedAvailable = await _bookCopyService.MarkAsAvailableAsync(copyId);
                    if (!copyMarkedAvailable)
                    {
                        await transaction.RollbackAsync();
                        return Result.Fail("Unable to mark the book copy as available.");
                    }

                    // Update borrowing status and return timestamp
                    borrowing.ReturnedAt = DateTime.UtcNow;
                    borrowing.Status = BorrowingStatus.Returned;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Result.Ok();
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    return Result.Fail("A concurrency conflict occurred while returning the book. Please try again.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            if (!result.Success)
            {
                return result;
            }

            // 4. Trigger waitlist promotion if a book ID exists
            if (bookId > 0)
            {
                await _waitlistService.PromoteNextInLineAsync(bookId);
            }

            // 5. Evaluate overdue status and invoke fine calculation if late
            if (borrowing.ReturnedAt.HasValue && borrowing.ReturnedAt.Value > borrowing.DueDate)
            {
                int overdueDays = (int)Math.Ceiling((borrowing.ReturnedAt.Value.Date - borrowing.DueDate.Date).TotalDays);
                if (overdueDays > 0)
                {
                    await _fineService.CalculateFineAsync(borrowing.Id);
                    return Result.Ok($"Book returned successfully. Note: This return is overdue by {overdueDays} day(s).");
                }
            }

            return Result.Ok("Book returned successfully.");
        }

        public async Task<IEnumerable<BorrowingSummaryDto>> GetMyBorrowingsAsync(string userId)
        {
            return await _context.Borrowings
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .Include(b => b.BookCopy)
                    .ThenInclude(c => c!.Book)
                .OrderByDescending(b => b.IssuedAt)
                .Select(b => new BorrowingSummaryDto
                {
                    Id = b.Id,
                    MemberName = b.User != null ? b.User.FullName : "Unknown",
                    MemberEmail = b.User != null ? b.User.Email ?? "" : "",
                    BookTitle = b.BookCopy != null && b.BookCopy.Book != null ? b.BookCopy.Book.Title : "Unknown Title",
                    CopyCode = b.BookCopy != null ? b.BookCopy.CopyCode : "Unknown Code",
                    IssuedAt = b.IssuedAt,
                    DueDate = b.DueDate,
                    ReturnedAt = b.ReturnedAt,
                    Status = b.Status.ToString()
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<BorrowingSummaryDto>> GetAllBorrowingsAsync()
        {
            return await _context.Borrowings
                .AsNoTracking()
                .Include(b => b.User)
                .Include(b => b.BookCopy)
                    .ThenInclude(c => c!.Book)
                .OrderByDescending(b => b.IssuedAt)
                .Select(b => new BorrowingSummaryDto
                {
                    Id = b.Id,
                    MemberName = b.User != null ? b.User.FullName : "Unknown",
                    MemberEmail = b.User != null ? b.User.Email ?? "" : "",
                    BookTitle = b.BookCopy != null && b.BookCopy.Book != null ? b.BookCopy.Book.Title : "Unknown Title",
                    CopyCode = b.BookCopy != null ? b.BookCopy.CopyCode : "Unknown Code",
                    IssuedAt = b.IssuedAt,
                    DueDate = b.DueDate,
                    ReturnedAt = b.ReturnedAt,
                    Status = b.Status.ToString()
                })
                .ToListAsync();
        }
    }
}