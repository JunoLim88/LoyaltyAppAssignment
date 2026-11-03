
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class AssignPointViewModel
    {
        [Required]
        [Range(1, 1000, ErrorMessage = "Enter between 1 and 1000 points.")]
        public int Point { get; set; }
    }
}
