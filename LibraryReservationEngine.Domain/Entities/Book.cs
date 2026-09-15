namespace LibraryReservationEngine.Domain.Entities
{
    // A Book is the TITLE, not a physical item. See BookCopy for the physical copies.
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;

        public int AuthorId { get; set; }
        public Author? Author { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public ICollection<BookCopy> BookCopies { get; set; } = new List<BookCopy>();
    }
}
