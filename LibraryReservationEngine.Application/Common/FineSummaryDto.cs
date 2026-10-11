using System;

namespace LibraryReservationEngine.Application.Common
{
    public class FineSummaryDto
    {
        public int Id { get; set; }
        public int BorrowingId { get; set; }
        public string MemberId { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string MemberEmail { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
        public string CopyCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int OverdueDays { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
