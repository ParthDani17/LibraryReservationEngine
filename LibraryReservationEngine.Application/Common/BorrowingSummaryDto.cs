using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryReservationEngine.Application.Common
{
    public class BorrowingSummaryDto
    {
        public int Id { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string CopyCode { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
