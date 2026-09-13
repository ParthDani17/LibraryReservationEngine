using LibraryReservationEngine.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Book> Books => Set<Book>();
        public DbSet<Author> Authors => Set<Author>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<BookCopy> BookCopies => Set<BookCopy>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
        public DbSet<Borrowing> Borrowings => Set<Borrowing>();
        public DbSet<Fine> Fines => Set<Fine>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Concurrency token for the "two people reserve the last copy" scenario (Phase 11).
            builder.Entity<BookCopy>()
                .Property(c => c.RowVersion)
                .IsRowVersion();

            builder.Entity<Book>()
                .HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Book>()
                .HasOne(b => b.Category)
                .WithMany(c => c.Books)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BookCopy>()
                .HasOne(c => c.Book)
                .WithMany(b => b.BookCopies)
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Borrowing>()
                .HasOne(br => br.Fine)
                .WithOne(f => f.Borrowing)
                .HasForeignKey<Fine>(f => f.BorrowingId);

            builder.Entity<Reservation>()
                .HasOne(r => r.BookCopy)
                .WithMany()
                .HasForeignKey(r => r.BookCopyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
