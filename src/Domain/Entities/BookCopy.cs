using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Domain.Entities
{
    // A single physical copy of a Book. This is what actually gets reserved/borrowed.
    public class BookCopy
    {
        public int Id { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }

        public string CopyCode { get; set; } = string.Empty; // e.g. "CLN-001"
        public BookCopyStatus Status { get; set; } = BookCopyStatus.Available;

        // Concurrency token: EF Core will use this to detect two simultaneous
        // updates to the same copy (Phase 11 - concurrency handling).
        public byte[]? RowVersion { get; set; }
    }
}
