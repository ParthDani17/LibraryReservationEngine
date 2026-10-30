using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize]
    public class WaitlistController : Controller
    {
        private readonly IWaitlistService _waitlistService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<WaitlistController> _logger;

        public WaitlistController(
            IWaitlistService waitlistService,
            UserManager<ApplicationUser> userManager,
            ILogger<WaitlistController> logger)
        {
            _waitlistService = waitlistService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> MyWaitlist()
        {
            var userId = _userManager.GetUserId(User)!;
            try
            {
                var entries = await _waitlistService.GetMyWaitlistEntriesAsync(userId);

                var models = entries.Select(e => new MyWaitlistEntryViewModel
                {
                    Id = e.Id,
                    BookTitle = e.BookTitle,
                    Position = e.Position,
                    JoinedAt = e.JoinedAt
                }).ToList();

                return View(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving waitlist entries for user {UserId}.", userId);
                TempData["Error"] = "Unable to load waitlist entries.";
                return View(new List<MyWaitlistEntryViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            try
            {
                var result = await _waitlistService.LeaveWaitlistAsync(id, userId);
                if (result.Success)
                {
                    _logger.LogInformation("User {UserId} left waitlist entry {WaitlistId}.", userId, id);
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to remove user {UserId} from waitlist entry {WaitlistId}: {Reason}", userId, id, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from waitlist {WaitlistId}.", userId, id);
                TempData["Error"] = "An error occurred while removing you from the waitlist.";
            }

            return RedirectToAction(nameof(MyWaitlist));
        }
    }
}
