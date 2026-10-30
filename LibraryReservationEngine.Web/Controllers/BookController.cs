using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    public class BookController : Controller
    {
        private readonly IBookService _bookService;
        private readonly ILogger<BookController> _logger;

        public BookController(IBookService bookService, ILogger<BookController> logger)
        {
            _bookService = bookService;
            _logger = logger;
        }

        // GET /Book
        public async Task<IActionResult> Index(string? query)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching books with query '{Query}'.", query);
                TempData["Error"] = "Unable to load book catalogue.";
                return View(Enumerable.Empty<BookListItemViewModel>());
            }
        }

        // GET /Book/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var book = await _bookService.GetBookDetailsAsync(id);
                if (book is null)
                {
                    _logger.LogWarning("Book details requested for non-existent id {BookId}.", id);
                    return NotFound();
                }
                return View(book);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving book details for id {BookId}.", id);
                return View("NotFound");
            }
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

            try
            {
                await _bookService.CreateBookAsync(model.Title, model.ISBN, model.AuthorName, model.CategoryName);
                _logger.LogInformation("Book '{Title}' created successfully.", model.Title);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating book '{Title}'.", model.Title);
                ModelState.AddModelError("", "An unexpected error occurred while creating the book.");
                return View(model);
            }
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

            try
            {
                var result = await _bookService.UpdateBookAsync(id, model.Title, model.ISBN, model.AuthorName, model.CategoryName);
                if (!result.Success)
                {
                    _logger.LogWarning("Failed to update book {BookId}: {Reason}", id, result.Message);
                    ModelState.AddModelError("", result.Message ?? "Update failed");
                    return View(model);
                }
                _logger.LogInformation("Book {BookId} updated successfully.", id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating book {BookId}.", id);
                ModelState.AddModelError("", "An unexpected error occurred while updating the book.");
                return View(model);
            }
        }

        // POST /Book/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _bookService.DeleteBookAsync(id);
                if (!result.Success)
                {
                    _logger.LogWarning("Failed to delete book {BookId}: {Reason}", id, result.Message);
                    TempData["Error"] = result.Message;
                }
                else
                {
                    _logger.LogInformation("Book {BookId} deleted.", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting book {BookId}.", id);
                TempData["Error"] = "An error occurred while deleting the book.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET /Book/History/5
        [Authorize(Roles = "Librarian")]
        public async Task<IActionResult> History(int id)
        {
            var book = await _bookService.GetBookDetailsAsync(id);
            if (book is null) return NotFound();

            try
            {
                var history = await _bookService.GetBorrowHistoryAsync(id);
                var models = history.Select(h => new BookBorrowHistoryViewModel
                {
                    StudentName = h.StudentName,
                    CopyCode = h.CopyCode,
                    IssuedAt = h.IssuedAt,
                    DueDate = h.DueDate,
                    ReturnedAt = h.ReturnedAt,
                    Status = h.Status
                }).ToList();

                ViewData["BookTitle"] = book.Title;
                ViewData["BookId"] = id;
                return View(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching borrow history for book {BookId}.", id);
                TempData["Error"] = "Unable to load borrow history.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
