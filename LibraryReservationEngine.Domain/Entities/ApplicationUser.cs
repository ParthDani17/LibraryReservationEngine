using Microsoft.AspNetCore.Identity;

namespace LibraryReservationEngine.Domain.Entities
{
    // Extends ASP.NET Core Identity's built-in user with our own fields.
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
    }
}
