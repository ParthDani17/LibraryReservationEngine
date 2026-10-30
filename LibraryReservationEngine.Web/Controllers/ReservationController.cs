using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LibraryReservationEngine.Domain.Entities;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly IReservationService _reservationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReservationController> _logger;

        public ReservationController(
            IReservationService reservationService,
            UserManager<ApplicationUser> userManager,
            ILogger<ReservationController> logger)
        {
            _reservationService = reservationService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET /Reservation (Librarian active reservation queue)
        [HttpGet]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var reservations = await _reservationService.GetActiveReservationsAsync();
                return View(reservations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading active reservations queue.");
                TempData["Error"] = "Unable to load active reservations.";
                return View(Enumerable.Empty<Application.Common.ReservationSummaryDto>());
            }
        }

        // POST /Reservation/Create
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookId)
        {
            var userId = _userManager.GetUserId(User)!;
            try
            {
                var result = await _reservationService.CreateReservationAsync(userId, bookId);
                if (result.Success)
                {
                    _logger.LogInformation("Reservation created by user {UserId} for book {BookId}.", userId, bookId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Reservation creation failed for user {UserId}, book {BookId}: {Reason}", userId, bookId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating reservation for user {UserId}, book {BookId}.", userId, bookId);
                TempData["Error"] = "An unexpected error occurred while placing your reservation.";
            }

            return RedirectToAction("Details", "Book", new { id = bookId });
        }

        // GET /Reservation/MyReservations
        public async Task<IActionResult> MyReservations()
        {
            var userId = _userManager.GetUserId(User)!;
            try
            {
                var reservations = await _reservationService.GetMyReservationsAsync(userId);

                var models = reservations.Select(r => new MyReservationViewModel
                {
                    Id = r.Id,
                    BookTitle = r.BookTitle,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ExpiresAt = r.ExpiresAt
                }).ToList();

                return View(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching reservations for user {UserId}.", userId);
                TempData["Error"] = "Unable to load your reservations.";
                return View(new List<MyReservationViewModel>());
            }
        }

        // POST /Reservation/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            try
            {
                var result = await _reservationService.CancelReservationAsync(id, userId);
                if (result.Success)
                {
                    _logger.LogInformation("Reservation {ReservationId} cancelled by user {UserId}.", id, userId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to cancel reservation {ReservationId} by user {UserId}: {Reason}", id, userId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error cancelling reservation {ReservationId} for user {UserId}.", id, userId);
                TempData["Error"] = "An error occurred while cancelling your reservation.";
            }

            return RedirectToAction(nameof(MyReservations));
        }
    }
}
