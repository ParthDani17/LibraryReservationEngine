using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    public class BookController : Controller
    {
        private readonly IBookService _bookService;

        public BookController(IBookService bookService)
        {
            _bookService = bookService;
        }

        // GET /Book
        public async Task<IActionResult> Index(string? query)
        {
            var books = await _bookService.SearchBooksAsync(query);
            var viewModels = books.Select(b => new BookListItemViewModel
            {
                Id = b.Id,
                Title = b.Title,
                AuthorName = b.Author?.Name ?? "",
                CategoryName = b.Category?.Name ?? "",
                AvailableCopies = b.BookCopies.Count(c => c.Status == Domain.Enums.BookCopyStatus.Available),
                TotalCopies = b.BookCopies.Count
            });
            ViewData["Query"] = query;
            return View(viewModels);
        }

        // GET /Book/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var book = await _bookService.GetBookDetailsAsync(id);
            if (book is null) return NotFound();
            return View(book);
        }

        // GET /Book/Create
        [Authorize(Roles = "Librarian")]
        public IActionResult Create() => View(new BookFormViewModel());

        // POST /Book/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Create(BookFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            await _bookService.CreateBookAsync(model.Title, model.ISBN, model.AuthorName, model.CategoryName);
            return RedirectToAction(nameof(Index));
        }

        // GET /Book/Edit/5
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Edit(int id)
        {
            var book = await _bookService.GetBookDetailsAsync(id);
            if (book is null) return NotFound();

            var model = new BookFormViewModel
            {
                Id = book.Id,
                Title = book.Title,
                ISBN = book.ISBN,
                AuthorName = book.Author?.Name ?? "",
                CategoryName = book.Category?.Name ?? ""
            };
            return View(model);
        }

        // POST /Book/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Edit(int id, BookFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _bookService.UpdateBookAsync(id, model.Title, model.ISBN, model.AuthorName, model.CategoryName);
            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message ?? "Update failed");
                return View(model);
            }
            return RedirectToAction(nameof(Index));
        }

        // POST /Book/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _bookService.DeleteBookAsync(id);
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}