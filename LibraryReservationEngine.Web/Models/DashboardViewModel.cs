namespace LibraryReservationEngine.Web.Models
{
    public class DashboardViewModel
    {
        public int TotalBooks { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public int BorrowedCopies { get; set; }
        public int ReservedCopies { get; set; }
        public int MaintenanceCopies { get; set; }
        public int ActiveReservations { get; set; }
        public int ActiveWaitlistEntries { get; set; }
        public int OverdueBorrowings { get; set; }
        public decimal TotalOutstandingFines { get; set; }
        public int TotalRegisteredMembers { get; set; }
    }
}