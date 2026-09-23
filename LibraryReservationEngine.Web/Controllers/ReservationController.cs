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

        public ReservationController(IReservationService reservationService, UserManager<ApplicationUser> userManager)
        {
            _reservationService = reservationService;
            _userManager = userManager;
        }

        // GET /Reservation (Librarian active reservation queue)
        [HttpGet]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Index()
        {
            var reservations = await _reservationService.GetActiveReservationsAsync();
            return View(reservations);
        }

        // POST /Reservation/Create
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookId)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _reservationService.CreateReservationAsync(userId, bookId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Details", "Book", new { id = bookId });
        }

        // GET /Reservation/MyReservations
        public async Task<IActionResult> MyReservations()
        {
            var userId = _userManager.GetUserId(User)!;
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

        // POST /Reservation/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _reservationService.CancelReservationAsync(id, userId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(MyReservations));
        }
    }
}