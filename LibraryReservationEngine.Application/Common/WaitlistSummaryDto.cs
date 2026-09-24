namespace LibraryReservationEngine.Application.Common
{
    public class WaitlistSummaryDto
    {
        public int Id { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public int Position { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }
}