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

        public WaitlistController(IWaitlistService waitlistService, UserManager<ApplicationUser> userManager)
        {
            _waitlistService = waitlistService;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyWaitlist()
        {
            var userId = _userManager.GetUserId(User)!;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _waitlistService.LeaveWaitlistAsync(id, userId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(MyWaitlist));
        }
    }
}