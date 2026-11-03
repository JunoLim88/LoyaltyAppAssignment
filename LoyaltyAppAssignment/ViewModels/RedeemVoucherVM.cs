
using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels
{
    public class RedeemVoucherViewModel
    {
        [Required]
        public string VoucherGUID { get; set; } = "";

        [Required]
        public string MemberPassword { get; set; } = "";

        [Required]
        public string OutletStaffPin { get; set; } = "";
    }
}
