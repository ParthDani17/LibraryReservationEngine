namespace LibraryReservationEngine.Web.Models
{
    public class BookCopyListViewModel
    {
        public int BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public List<BookCopyItemViewModel> Copies { get; set; } = new();
    }

    public class BookCopyItemViewModel
    {
        public int Id { get; set; }
        public string CopyCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class AddCopyViewModel
    {
        public int BookId { get; set; }
        public string CopyCode { get; set; } = string.Empty;
    }
}