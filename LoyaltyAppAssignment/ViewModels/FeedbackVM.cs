
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class FeedbackViewModel
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = "";

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = "";

        [Range(1, 5, ErrorMessage = "Please select a rating.")]
        public int Rating { get; set; }

        [Required]
        public string Category { get; set; } = "";

        [Required]
        public string Location { get; set; } = "";
    }
}
