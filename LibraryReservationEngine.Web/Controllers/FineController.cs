using System.Security.Claims;
using LibraryReservationEngine.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize]
    public class FineController : Controller
    {
        private readonly IFineService _fineService;
        private readonly ILogger<FineController> _logger;

        public FineController(IFineService fineService, ILogger<FineController> logger)
        {
            _fineService = fineService;
            _logger = logger;
        }

        // GET: /Fine/MyFines (Student fine history)
        [HttpGet]
        public async Task<IActionResult> MyFines()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized access attempt to MyFines.");
                return Unauthorized();
            }

            try
            {
                var fines = await _fineService.GetMyFinesAsync(userId);
                return View(fines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fines for user {UserId}.", userId);
                TempData["Error"] = "Unable to load your fine history.";
                return View(Enumerable.Empty<Application.Common.FineSummaryDto>());
            }
        }

        // GET: /Fine/Index (Librarian all fines management)
        [HttpGet]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var fines = await _fineService.GetAllFinesAsync();
                return View(fines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all library fines.");
                TempData["Error"] = "Unable to load library fines.";
                return View(Enumerable.Empty<Application.Common.FineSummaryDto>());
            }
        }

        // POST: /Fine/MarkAsPaid
        [HttpPost]
        [Authorize(Roles = "Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int fineId, string? returnUrl = null)
        {
            try
            {
                var result = await _fineService.MarkFineAsPaidAsync(fineId);
                if (result.Success)
                {
                    _logger.LogInformation("Librarian marked fine {FineId} as paid.", fineId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to mark fine {FineId} as paid: {Reason}", fineId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error marking fine {FineId} as paid.", fineId);
                TempData["Error"] = "An error occurred while settling the fine.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Fine/Waive
        [HttpPost]
        [Authorize(Roles = "Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Waive(int fineId, string? returnUrl = null)
        {
            try
            {
                var result = await _fineService.WaiveFineAsync(fineId);
                if (result.Success)
                {
                    _logger.LogInformation("Librarian waived fine {FineId}.", fineId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to waive fine {FineId}: {Reason}", fineId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error waiving fine {FineId}.", fineId);
                TempData["Error"] = "An error occurred while waiving the fine.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Fine/Pay
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int fineId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized payment attempt for fine {FineId}.", fineId);
                return Unauthorized();
            }

            try
            {
                var result = await _fineService.PayFineAsync(fineId, userId);
                if (result.Success)
                {
                    _logger.LogInformation("Fine {FineId} paid by student {UserId}.", fineId, userId);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Fine payment failed for {FineId} by student {UserId}: {Reason}", fineId, userId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing payment for fine {FineId} by user {UserId}.", fineId, userId);
                TempData["Error"] = "An error occurred while processing your fine payment.";
            }

            return RedirectToAction(nameof(MyFines));
        }
    }
}
