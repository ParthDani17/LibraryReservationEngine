using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Domain.Entities
{
    public class WaitlistEntry
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }

        public int Position { get; set; } // for FIFO ordering
        public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
