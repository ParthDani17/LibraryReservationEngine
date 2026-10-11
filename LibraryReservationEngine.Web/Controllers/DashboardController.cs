using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize(Roles = "Librarian")]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            var stats = await _dashboardService.GetStatsAsync();

            var model = new DashboardViewModel
            {
                TotalBooks = stats.TotalBooks,
                TotalCopies = stats.TotalCopies,
                AvailableCopies = stats.AvailableCopies,
                BorrowedCopies = stats.BorrowedCopies,
                ReservedCopies = stats.ReservedCopies,
                MaintenanceCopies = stats.MaintenanceCopies,
                ActiveReservations = stats.ActiveReservations,
                ActiveWaitlistEntries = stats.ActiveWaitlistEntries,
                OverdueBorrowings = stats.OverdueBorrowings,
                TotalOutstandingFines = stats.TotalOutstandingFines,
                TotalRegisteredMembers = stats.TotalRegisteredMembers
            };

            return View(model);
        }
    }
}