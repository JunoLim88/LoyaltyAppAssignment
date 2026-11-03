
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string PhoneNumber { get; set; } = "";

        [Required]
        public string Otp { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = "";
    }
}
