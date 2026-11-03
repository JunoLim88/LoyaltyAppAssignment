
namespace LoyaltyAppAssignment.ViewModels
{
    public class ProfileViewModel
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public int Point { get; set; }
        public string Tier { get; set; } = "";
        public string ReferralCode { get; set; } = "";
        public int AccountStatus { get; set; }
    }
}
