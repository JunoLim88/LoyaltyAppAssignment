
namespace LoyaltyAppAssignment.ViewModels
{
    public class HistoryViewModel
    {
        public int RecordID { get; set; }
        public int RecordType { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public int? Points { get; set; }

        public HistoryVoucherViewModel? VoucherDetail { get; set; }
        public HistoryRewardViewModel? RewardDetail { get; set; }
        public HistoryPointViewModel? PointDetail { get; set; }
    }

    public class HistoryVoucherViewModel
    {
        public string? VoucherName { get; set; }
        public string? Title { get; set; }
    }

    public class HistoryRewardViewModel
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public int? Point { get; set; }
    }

    public class HistoryPointViewModel
    {
        public int? Point { get; set; }
        public string? Description { get; set; }
    }
}
