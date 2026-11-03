
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter your phone number")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "Please enter your password")]
        public string Password { get; set; } = "";
    }
}
