using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Domain.Entities
{
    public class Reservation
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }

        // Assigned once a specific copy is held for this reservation.
        public int? BookCopyId { get; set; }
        public BookCopy? BookCopy { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}
