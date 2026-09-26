using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class BookCopyService : IBookCopyService
    {
        private readonly ApplicationDbContext _context;

        public BookCopyService(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- Interface methods (contract used by Reservation/Borrowing/Return) ---

        public async Task<bool> MarkAsBorrowedAsync(int copyId)
        {
            var copy = await _context.BookCopies.FindAsync(copyId);
            if (copy is null || copy.Status != BookCopyStatus.Reserved) return false;
            copy.Status = BookCopyStatus.Borrowed;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAsAvailableAsync(int copyId)
        {
            var copy = await _context.BookCopies.FindAsync(copyId);
            if (copy is null) return false;
            copy.Status = BookCopyStatus.Available;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAsReservedAsync(int copyId)
        {
            var copy = await _context.BookCopies.FindAsync(copyId);
            if (copy is null || copy.Status != BookCopyStatus.Available) return false;

            copy.Status = BookCopyStatus.Reserved;
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Two requests raced for the same copy — this one loses.
                // Fully handled/tested in feature/concurrency.
                return false;
            }
        }

        public async Task<int?> FindAvailableCopyIdAsync(int bookId)
        {
            var copy = await _context.BookCopies
                .Where(c => c.BookId == bookId && c.Status == BookCopyStatus.Available)
                .FirstOrDefaultAsync();
            return copy?.Id;
        }

        // --- Extra methods, used only by the Librarian copy-management UI ---

        public async Task<List<BookCopy>> GetCopiesForBookAsync(int bookId)
        {
            return await _context.BookCopies
                .Where(c => c.BookId == bookId)
                .OrderBy(c => c.CopyCode)
                .ToListAsync();
        }

        public async Task<BookCopy> AddCopyAsync(int bookId, string copyCode)
        {
            var copy = new BookCopy
            {
                BookId = bookId,
                CopyCode = copyCode,
                Status = BookCopyStatus.Available
            };
            _context.BookCopies.Add(copy);
            await _context.SaveChangesAsync();
            return copy;
        }

        public async Task<bool> SetMaintenanceAsync(int copyId)
        {
            var copy = await _context.BookCopies.FindAsync(copyId);
            if (copy is null || copy.Status == BookCopyStatus.Borrowed || copy.Status == BookCopyStatus.Reserved)
                return false; // can't send a copy to maintenance while it's out with someone

            copy.Status = BookCopyStatus.Maintenance;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCopyAsync(int copyId)
        {
            var copy = await _context.BookCopies.FindAsync(copyId);
            if (copy is null) return false;
            if (copy.Status == BookCopyStatus.Borrowed || copy.Status == BookCopyStatus.Reserved)
                return false; // can't delete a copy that's out with someone

            bool hasReservationHistory = await _context.Reservations.AnyAsync(r => r.BookCopyId == copyId);
            if (hasReservationHistory)
                return false; // has past reservations — deleting would break the FK, and would lose history anyway

            _context.BookCopies.Remove(copy);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}