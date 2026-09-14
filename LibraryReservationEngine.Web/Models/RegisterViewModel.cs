using System.ComponentModel.DataAnnotations;

namespace LibraryReservationEngine.Web.Models
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string Fullname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(8)]
        public string Password {  get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword {  get; set; } = string.Empty;
    }
}
