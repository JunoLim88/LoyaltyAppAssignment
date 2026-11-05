using System.ComponentModel.DataAnnotations;

namespace LoyaltyAppAssignment.ViewModels.Admin;

public class AdminLoginInput
{
    [Required, StringLength(100)] public string Username { get; set; } = "";
    [Required, StringLength(200)] public string Password { get; set; } = "";
}

public class MemberInput
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, RegularExpression(@"^0\d{8,10}$", ErrorMessage = "Enter a phone number starting with 0 (9–11 digits).")]
    public string PhoneNumber { get; set; } = "";
    [Required, DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
    [StringLength(200, MinimumLength = 6)] public string? Password { get; set; }
    [StringLength(50)] public string? Tier { get; set; }
    [StringLength(100)] public string? ReferralBy { get; set; }
}

public class CatalogInput
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [StringLength(150)] public string? Name { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(300)] public string? SubTitle { get; set; }
    [StringLength(150)] public string? ShortTitle { get; set; }
    [StringLength(10000)] public string? Description { get; set; }
    [StringLength(10000)] public string? TnC { get; set; }
    [Range(0, int.MaxValue)] public int? Point { get; set; }
    [Required] public DateTime? StartDate { get; set; }
    [Required] public DateTime? EndDate { get; set; }
    [Required, RegularExpression("^(Active|Deactive|Expired)$")] public string Status { get; set; } = "Active";
    [StringLength(20000)] public string VoucherRules { get; set; } = "[]";
    public IFormFile? File { get; set; }
}

public class OutletInput
{
    [Required, StringLength(150)] public string OutletName { get; set; } = "";
}

public class StaffInput
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(150)] public string OutletStaffName { get; set; } = "";
    [Range(1, int.MaxValue)] public int OutletID { get; set; }
    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter a 6-digit staff PIN.")]
    public string Pin { get; set; } = "";
    [StringLength(100)] public string? Role { get; set; }
}

public class AssignPointsInput
{
    [Required, RegularExpression(@"^0\d{8,10}$")] public string PhoneNumber { get; set; } = "";
    [Range(1, int.MaxValue)] public int OutletID { get; set; }
    [Required, RegularExpression(@"^\d{6}$")] public string Pin { get; set; } = "";
    [Range(1, int.MaxValue)] public int Point { get; set; }
}

public class RejectPointInput
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required, RegularExpression(@"^\d{6}$")] public string Pin { get; set; } = "";
    [Range(1, int.MaxValue)] public int OutletID { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public class VoucherRuleInput
{
    public int? VoucherRuleID { get; set; }
    public int VoucherID { get; set; }
    public string? DayOfWeek { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}
