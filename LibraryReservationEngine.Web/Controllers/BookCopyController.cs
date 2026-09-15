using LibraryReservationEngine.Infrastructure.Services;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    [Authorize(Roles = "Librarian")]
    public class BookCopyController : Controller
    {
        private readonly BookCopyService _bookCopyService;
        private readonly Application.Interfaces.IBookService _bookService;

        public BookCopyController(BookCopyService bookCopyService, Application.Interfaces.IBookService bookService)
        {
            _bookCopyService = bookCopyService;
            _bookService = bookService;
        }

        // GET /BookCopy/Index/5   (5 = bookId)
        public async Task<IActionResult> Index(int bookId)
        {
            var book = await _bookService.GetBookDetailsAsync(bookId);
            if (book is null) return NotFound();

            var copies = await _bookCopyService.GetCopiesForBookAsync(bookId);

            var model = new BookCopyListViewModel
            {
                BookId = bookId,
                BookTitle = book.Title,
                Copies = copies.Select(c => new BookCopyItemViewModel
                {
                    Id = c.Id,
                    CopyCode = c.CopyCode,
                    Status = c.Status.ToString()
                }).ToList()
            };

            return View(model);
        }

        // POST /BookCopy/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddCopyViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.CopyCode))
            {
                await _bookCopyService.AddCopyAsync(model.BookId, model.CopyCode);
            }
            return RedirectToAction(nameof(Index), new { bookId = model.BookId });
        }

        // POST /BookCopy/SetMaintenance/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMaintenance(int id, int bookId)
        {
            var success = await _bookCopyService.SetMaintenanceAsync(id);
            if (!success)
            {
                TempData["Error"] = "Cannot set to maintenance — copy is currently reserved or borrowed.";
            }
            return RedirectToAction(nameof(Index), new { bookId });
        }

        // POST /BookCopy/SetAvailable/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAvailable(int id, int bookId)
        {
            await _bookCopyService.MarkAsAvailableAsync(id);
            return RedirectToAction(nameof(Index), new { bookId });
        }

        // POST /BookCopy/Delete/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int bookId)
        {
            var success = await _bookCopyService.DeleteCopyAsync(id);
            if (!success)
            {
                TempData["Error"] = "Cannot delete — copy is currently reserved or borrowed.";
            }
            return RedirectToAction(nameof(Index), new { bookId });
        }
    }
}