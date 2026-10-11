namespace LibraryReservationEngine.Web.Models
{
    public class MyWaitlistEntryViewModel
    {
        public int Id { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public int Position { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}