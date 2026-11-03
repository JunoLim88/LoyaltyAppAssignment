
namespace LoyaltyAppAssignment.ViewModels
{
    public class VoucherViewModel
    {
        public int VoucherID { get; set; }
        public string? VoucherGUID { get; set; }
        public string? VoucherName { get; set; }
        public string? Image { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? ShortTitle { get; set; }
        public string? Description { get; set; }
        public string? TnC { get; set; }
        public string? VoucherStatus { get; set; }
        public DateTime? ExpiredDate { get; set; }
    }
}
