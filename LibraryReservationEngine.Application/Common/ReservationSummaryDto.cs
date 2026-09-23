namespace LibraryReservationEngine.Application.Common
{
    public class ReservationSummaryDto
    {
        public int Id { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MemberEmail { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
        public string CopyCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}   