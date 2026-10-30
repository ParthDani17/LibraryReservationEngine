using System.Security.Claims;
using LibraryReservationEngine.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize]
    public class BorrowingController : Controller
    {
        private readonly IBorrowingService _borrowingService;
        private readonly ILogger<BorrowingController> _logger;

        public BorrowingController(IBorrowingService borrowingService, ILogger<BorrowingController> logger)
        {
            _borrowingService = borrowingService;
            _logger = logger;
        }

        // GET: /Borrowing (Librarian: view borrowings of all students)
        [HttpGet]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var borrowings = await _borrowingService.GetAllBorrowingsAsync();
                return View(borrowings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all borrowings.");
                TempData["Error"] = "Unable to load borrowings. Please try again.";
                return View(Enumerable.Empty<Application.Common.BorrowingSummaryDto>());
            }
        }

        // POST: /Borrowing/Issue
        [HttpPost]
        [Authorize(Roles = "Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(int reservationId, string? returnUrl = null)
        {
            try
            {
                var result = await _borrowingService.IssueBookAsync(reservationId);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully issued book for reservation {ReservationId}.", reservationId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to issue book for reservation {ReservationId}: {Reason}", reservationId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while issuing book for reservation {ReservationId}.", reservationId);
                TempData["Error"] = "An unexpected error occurred while issuing the book.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Reservation");
        }

        // POST: /Borrowing/Return
        [HttpPost]
        [Authorize(Roles = "Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int borrowingId, string? returnUrl = null)
        {
            try
            {
                var result = await _borrowingService.ReturnBookAsync(borrowingId);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully returned book for borrowing record {BorrowingId}.", borrowingId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to return book for borrowing record {BorrowingId}: {Reason}", borrowingId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while returning borrowing {BorrowingId}.", borrowingId);
                TempData["Error"] = "An unexpected error occurred while returning the book.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Borrowing/MyBorrowings
        [HttpGet]
        public async Task<IActionResult> MyBorrowings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized access attempt to MyBorrowings.");
                return Unauthorized();
            }

            try
            {
                var borrowings = await _borrowingService.GetMyBorrowingsAsync(userId);
                return View(borrowings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching borrowings for user {UserId}.", userId);
                TempData["Error"] = "Unable to load your borrowing history.";
                return View(Enumerable.Empty<Application.Common.BorrowingSummaryDto>());
            }
        }
    }
}
