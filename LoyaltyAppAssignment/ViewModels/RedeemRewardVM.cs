
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class RedeemRewardViewModel
    {
        [Required]
        public string RewardGUID { get; set; } = "";

        [Required]
        public string MemberPassword { get; set; } = "";

        [Required]
        public string OutletStaffPin { get; set; } = "";
    }
}
