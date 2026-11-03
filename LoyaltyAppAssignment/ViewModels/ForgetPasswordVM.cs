
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required]
        public string PhoneNumber { get; set; } = "";
    }
}
