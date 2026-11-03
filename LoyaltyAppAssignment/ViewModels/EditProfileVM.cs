
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class EditProfileViewModel
    {
        [Required]
        public string Name { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
    }
}
