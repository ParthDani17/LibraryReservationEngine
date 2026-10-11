namespace LibraryReservationEngine.Application.Common
{
    public class BookBorrowHistoryDto
    {
        public string StudentName { get; set; } = string.Empty;
        public string CopyCode { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}