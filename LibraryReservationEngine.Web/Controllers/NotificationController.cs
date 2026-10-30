using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationController> logger)
        {
            _notificationService = notificationService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /Notification (or /Notification/Index)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized access attempt to notifications.");
                return Unauthorized();
            }

            try
            {
                var notifications = await _notificationService.GetMyNotificationsAsync(userId);
                return View(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications for user {UserId}.", userId);
                TempData["Error"] = "Unable to load notifications.";
                return View(Enumerable.Empty<Application.Common.NotificationSummaryDto>());
            }
        }

        // POST: /Notification/MarkAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var result = await _notificationService.MarkAsReadAsync(id, userId);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                }
                else
                {
                    _logger.LogWarning("Failed to mark notification {NotificationId} read for user {UserId}: {Reason}", id, userId, result.Message);
                    TempData["Error"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read.", id);
                TempData["Error"] = "An error occurred while updating the notification.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Notification/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead(string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var result = await _notificationService.MarkAllAsReadAsync(userId);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications read for user {UserId}.", userId);
                TempData["Error"] = "An error occurred while marking all notifications as read.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Notification/GetUnreadCount
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { count = 0 });
            }

            try
            {
                var count = await _notificationService.GetUnreadCountAsync(userId);
                return Json(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unread notification count for user {UserId}.", userId);
                return Json(new { count = 0 });
            }
        }
    }
}
