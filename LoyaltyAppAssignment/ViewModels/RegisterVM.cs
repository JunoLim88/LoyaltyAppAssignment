
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string Name { get; set; } = "";

        [Required]
        [RegularExpression(@"^0\d{8,10}$",
            ErrorMessage = "Phone number must start with 0.")]
        public string PhoneNumber { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";

        [Required]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public DateTime Birthday { get; set; }
    }
}
