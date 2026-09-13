using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Domain.Entities
{
    public class Borrowing
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int BookCopyId { get; set; }
        public BookCopy? BookCopy { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }
        public DateTime? ReturnedAt { get; set; }

        public BorrowingStatus Status { get; set; } = BorrowingStatus.Active;

        public Fine? Fine { get; set; }
    }
}
