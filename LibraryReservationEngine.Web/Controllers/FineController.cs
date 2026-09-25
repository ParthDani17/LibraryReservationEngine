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

        public FineController(IFineService fineService)
        {
            _fineService = fineService;
        }

        // GET: /Fine/MyFines (Student fine history)
        [HttpGet]
        public async Task<IActionResult> MyFines()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var fines = await _fineService.GetMyFinesAsync(userId);
            return View(fines);
        }

        // GET: /Fine/Index (Librarian all fines management)
        [HttpGet]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Index()
        {
            var fines = await _fineService.GetAllFinesAsync();
            return View(fines);
        }

        // POST: /Fine/MarkAsPaid
        [HttpPost]
        [Authorize(Roles = "Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int fineId, string? returnUrl = null)
        {
            var result = await _fineService.MarkFineAsPaidAsync(fineId);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
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
            var result = await _fineService.WaiveFineAsync(fineId);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
