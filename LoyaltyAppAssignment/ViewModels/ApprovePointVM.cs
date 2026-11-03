
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class ApprovePointViewModel
    {
        [Required]
        public string Code { get; set; } = "";

        [Required]
        public string OutletGUID { get; set; } = "";

        [Required]
        public string Pin { get; set; } = "";

        [Range(1, 1000)]
        public int Point { get; set; }
    }
}
