using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Domain.Entities
{
    public class Fine
    {
        public int Id { get; set; }

        public int BorrowingId { get; set; }
        public Borrowing? Borrowing { get; set; }

        public decimal Amount { get; set; }
        public int OverdueDays { get; set; }
        public FineStatus Status { get; set; } = FineStatus.Unpaid;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
