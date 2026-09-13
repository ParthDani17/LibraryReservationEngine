namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person A. Person B's Borrowing/Return features code against
    // this interface without needing Person A's implementation to exist yet.
    public interface IBookCopyService
    {
        Task<bool> MarkAsBorrowedAsync(int copyId);
        Task<bool> MarkAsAvailableAsync(int copyId);
        Task<bool> MarkAsReservedAsync(int copyId);
        Task<int?> FindAvailableCopyIdAsync(int bookId);
    }
}
