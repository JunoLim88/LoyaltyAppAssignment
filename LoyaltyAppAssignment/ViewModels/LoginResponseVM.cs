
namespace LoyaltyAppAssignment.ViewModels
{
    public class LoginResponseViewModel
    {
        public string SuccessToken { get; set; } = "";
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
        public DateTime? OtpCreated { get; set; }
    }
}
