
namespace LoyaltyAppAssignment.ViewModels
{
    public class VoucherListViewModel
    {
        public List<VoucherViewModel> ActiveVouchers { get; set; } = new();

        public List<VoucherViewModel> ClaimedVouchers { get; set; } = new();

        public List<VoucherViewModel> ExpiredVouchers { get; set; } = new();
    }
}
