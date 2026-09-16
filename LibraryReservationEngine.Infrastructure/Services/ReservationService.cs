using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class ReservationService : IReservationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookCopyService _bookCopyService;
        private readonly IWaitlistService _waitlistService;
        private readonly INotificationService _notificationService;

        private const int HoldPeriodHours = 24;
        private const int MaxClaimRetries = 3;

        public ReservationService(
            ApplicationDbContext context,
            IBookCopyService bookCopyService,
            IWaitlistService waitlistService,
            INotificationService notificationService)
        {
            _context = context;
            _bookCopyService = bookCopyService;
            _waitlistService = waitlistService;
            _notificationService = notificationService;
        }

        public async Task<Result> CreateReservationAsync(string userId, int bookId)
        {
            // Step 1 — check for an existing active reservation on the same book
            bool alreadyReserved = await _context.Reservations.AnyAsync(r =>
                r.UserId == userId &&
                r.BookId == bookId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Active));

            if (alreadyReserved)
                return Result.Fail("You already have a reservation for this book.");

            // Step 2 — try to claim an available copy, with a few retries in case of a race
            for (int attempt = 0; attempt < MaxClaimRetries; attempt++)
            {
                var copyId = await _bookCopyService.FindAvailableCopyIdAsync(bookId);

                if (copyId is null)
                {
                    // No copy free at all — send them to the waitlist instead
                    return await _waitlistService.JoinWaitlistAsync(userId, bookId);
                }

                bool claimed = await _bookCopyService.MarkAsReservedAsync(copyId.Value);

                if (claimed)
                {
                    var reservation = new Reservation
                    {
                        UserId = userId,
                        BookId = bookId,
                        BookCopyId = copyId.Value,
                        Status = ReservationStatus.Active,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddHours(HoldPeriodHours)
                    };

                    _context.Reservations.Add(reservation);
                    await _context.SaveChangesAsync();

                    await _notificationService.SendAsync(
                        userId, NotificationType.ReservationReady,
                        "Your reservation is ready for pickup.");

                    return Result.Ok("Reservation created successfully.");
                }

                // Someone else grabbed that exact copy between Find and Mark — loop and try again
            }

            // Exhausted retries — fall back to waitlist rather than failing outright
            return await _waitlistService.JoinWaitlistAsync(userId, bookId);
        }

        public async Task<Result> CancelReservationAsync(int reservationId, string userId)
        {
            var reservation = await _context.Reservations.FindAsync(reservationId);
            if (reservation is null) return Result.Fail("Reservation not found.");
            if (reservation.UserId != userId) return Result.Fail("This isn't your reservation.");
            if (reservation.Status != ReservationStatus.Active && reservation.Status != ReservationStatus.Pending)
                return Result.Fail("This reservation can no longer be cancelled.");

            reservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync();

            if (reservation.BookCopyId.HasValue)
            {
                await _bookCopyService.MarkAsAvailableAsync(reservation.BookCopyId.Value);
                await _waitlistService.PromoteNextInLineAsync(reservation.BookId);
            }

            return Result.Ok("Reservation cancelled.");
        }

        public async Task<IEnumerable<ReservationSummaryDto>> GetMyReservationsAsync(string userId)
        {
            return await _context.Reservations
                .Where(r => r.UserId == userId)
                .Include(r => r.Book)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReservationSummaryDto
                {
                    Id = r.Id,
                    BookTitle = r.Book!.Title,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt,
                    ExpiresAt = r.ExpiresAt
                })
                .ToListAsync();
        }
    }
}