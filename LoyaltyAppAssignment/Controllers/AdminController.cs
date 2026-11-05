using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoyaltyAppAssignment.Services;
using LoyaltyAppAssignment.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LoyaltyAppAssignment.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[AutoValidateAntiforgeryToken]
public class AdminController(AdminApiClient api, ILogger<AdminController> logger) : Controller
{
    private string? Token => HttpContext.Session.GetString("AdminToken");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!string.Equals(context.ActionDescriptor.RouteValues["action"], "Login", StringComparison.OrdinalIgnoreCase)
            && HttpContext.Session.GetInt32("AdminID") is null)
        {
            context.Result = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                ? Unauthorized(new { message = "Your admin session has expired. Please sign in again." })
                : RedirectToAction(nameof(Login));
        }
        base.OnActionExecuting(context);
    }

    [HttpGet]
    public IActionResult Login() => HttpContext.Session.GetInt32("AdminID") is null ? View() : RedirectToAction(nameof(Dashboard));

    [HttpPost]
    public async Task<IActionResult> Login(AdminLoginInput input)
    {
        if (!ModelState.IsValid) return InvalidInput();
        try
        {
            var result = await api.SendAsync("api/Admin/AdminLogin", HttpMethod.Post,
                JsonContent.Create(new { username = input.Username.Trim(), password = input.Password }),
                cancellationToken: HttpContext.RequestAborted);
            var data = AdminApiClient.ForBrowser(result);
            if (!int.TryParse(data?["adminID"]?.ToString(), out var id) || id <= 0)
                return BadRequest(new { message = "The API returned an invalid administrator account." });
            HttpContext.Session.SetInt32("AdminID", id);
            HttpContext.Session.SetString("AdminName", input.Username.Trim());
            // The guide's deployment returns an ID only; the supplied API source also supports JWT.
            var token = result is JsonObject obj ? obj.FirstOrDefault(p => p.Key.Equals("successToken", StringComparison.OrdinalIgnoreCase)).Value?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(token)) HttpContext.Session.SetString("AdminToken", token);
            else HttpContext.Session.Remove("AdminToken");
            return Json(new { success = true, redirectUrl = Url.Action(nameof(Dashboard)) });
        }
        catch (AdminApiException ex) { return ApiError(ex, false); }
    }

    [HttpPost]
    public IActionResult Logout()
    {
        ClearAdminSession();
        return RedirectToAction(nameof(Login));
    }

    private IActionResult Page(string section)
    {
        ViewData["Section"] = section;
        return View("Index");
    }
    [HttpGet] public IActionResult Index() => RedirectToAction(nameof(Dashboard));
    [HttpGet] public IActionResult Dashboard() => Page("dashboard");
    [HttpGet] public IActionResult Members() => Page("members");
    [HttpGet] public IActionResult Rewards() => Page("rewards");
    [HttpGet] public IActionResult Vouchers() => Page("vouchers");
    [HttpGet] public IActionResult Highlights() => Page("highlights");
    [HttpGet] public IActionResult Outlets() => Page("outlets");
    [HttpGet] public IActionResult Staff() => Page("staff");
    [HttpGet] public IActionResult Points() => Page("points");
    [HttpGet] public IActionResult Redemptions() => Page("redemptions");
    [HttpGet] public IActionResult Feedback() => Page("feedback");

    [HttpGet]
    public async Task<IActionResult> Data(string section, int? outletID, int? memberID)
    {
        if (outletID is <= 0 || memberID is <= 0) return BadRequest(new { message = "Choose a valid filter." });
        var path = section switch
        {
            "members" => "api/ManageMember/GetAllMember",
            "rewards" => "api/ManageRewards/GetAllRewards",
            "vouchers" => "api/ManageRewards/GetAllVouchers",
            "highlights" => "api/ManageHighlight/GetAllHighlight",
            "outlets" => "api/Outlet/GetAllOutlets",
            "staff" when outletID.HasValue => "api/OutletStaff/GetAllOutletStaff",
            "points" => outletID.HasValue ? $"api/ManagePoint/GetAllPointAssignRecordByOutletId/{outletID}" : "api/ManagePoint/GetAllPointAssignRecord",
            "rewardRecords" => "api/ManageRecords/GetAllRewardAssignRecord",
            "voucherRecords" => "api/ManageRecords/GetAllVoucherAssignRecord",
            "feedback" => memberID.HasValue ? "api/Feedback/GetFeedbackListFilterUser" : "api/Feedback/GetAllFeedbackList",
            _ => null
        };
        if (path is null) return BadRequest(new { message = "Choose an available section and outlet." });
        HttpContent? content = section == "staff" ? JsonContent.Create(new { outletID })
            : section == "feedback" && memberID.HasValue ? JsonContent.Create(new { memberID }) : null;
        return await Execute(path, content is null ? HttpMethod.Get : HttpMethod.Post, content);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(string section, int id)
    {
        if (id <= 0) return BadRequest(new { message = "Choose a valid record." });
        var path = section switch
        {
            "members" => $"api/ManageMember/FindMemberById?id={id}",
            "rewards" => $"api/ManageRewards/GetReward/{id}",
            "vouchers" => $"api/ManageRewards/GetVoucher/{id}",
            "highlights" => $"api/ManageHighlight/GetHighlight/{id}",
            "outlets" => "api/Outlet/GetOutlet",
            "staff" => $"api/OutletStaff/GetOutletStaff/{id}",
            "points" => $"api/ManagePoint/GetAllPointAssignRecordByPointId/{id}",
            "rewardRecords" => $"api/ManageRecords/GetRewardAssignRecordById/{id}",
            "voucherRecords" => $"api/ManageRecords/GetVoucherAssignRecordById/{id}",
            "feedback" => $"api/Feedback/GetFeedback/{id}",
            _ => null
        };
        if (path is null) return BadRequest(new { message = "Unknown record type." });
        return await Execute(path, section == "outlets" ? HttpMethod.Post : HttpMethod.Get,
            section == "outlets" ? JsonContent.Create(new { outletID = id }) : null);
    }

    [HttpPost]
    public async Task<IActionResult> SaveMember(MemberInput input)
    {
        if (input.Id == 0 && string.IsNullOrWhiteSpace(input.Password)) ModelState.AddModelError("Password", "A password is required for a new member.");
        if (input.BirthDate > DateTime.Today) ModelState.AddModelError("BirthDate", "Birth date cannot be in the future.");
        if (!ModelState.IsValid) return InvalidInput();
        var payload = new JsonObject
        {
            ["memberID"] = input.Id, ["name"] = input.Name.Trim(), ["email"] = input.Email.Trim(),
            ["phoneNumber"] = input.PhoneNumber, ["birthDate"] = input.BirthDate!.Value.ToString("yyyy-MM-dd"),
            ["tier"] = input.Tier, ["referralBy"] = input.ReferralBy
        };
        if (input.Id == 0) payload["password"] = input.Password;
        return await Execute("api/ManageMember/" + (input.Id == 0 ? "CreateMember" : "EditMember"), HttpMethod.Post, JsonContent.Create(payload), true);
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SaveCatalog(string kind, CatalogInput input)
    {
        if (kind is not ("rewards" or "vouchers" or "highlights")) return BadRequest(new { message = "Unknown catalog." });
        if (kind != "highlights" && string.IsNullOrWhiteSpace(input.Name)) ModelState.AddModelError("Name", "Name is required.");
        if (kind == "rewards" && !input.Point.HasValue) ModelState.AddModelError("Point", "Points are required.");
        if (input.StartDate > input.EndDate) ModelState.AddModelError("EndDate", "End date must be on or after start date.");
        if (kind == "highlights" && input.Id == 0 && input.File is null) ModelState.AddModelError("File", "Upload an image for the promotion.");
        ValidateImage(input.File);
        List<VoucherRuleInput> rules = [];
        if (kind == "vouchers")
        {
            try
            {
                rules = JsonSerializer.Deserialize<List<VoucherRuleInput>>(input.VoucherRules, JsonOptions) ?? [];
                if (rules.Count > 7 || rules.Select(r => r.DayOfWeek).Distinct().Count() != rules.Count || rules.Any(r =>
                    !Enum.GetNames<DayOfWeek>().Contains(r.DayOfWeek) || r.StartTime is null || r.EndTime is null ||
                    r.StartTime < TimeSpan.Zero || r.EndTime >= TimeSpan.FromDays(1) || r.StartTime >= r.EndTime || r.VoucherRuleID < 0))
                    ModelState.AddModelError("VoucherRules", "Choose unique days and valid opening and closing times.");
                foreach (var rule in rules) rule.VoucherID = input.Id;
            }
            catch (JsonException) { ModelState.AddModelError("VoucherRules", "Voucher rules are invalid."); }
        }
        if (!ModelState.IsValid) return InvalidInput();

        var fields = new Dictionary<string, string?>
        {
            ["Title"] = input.Title.Trim(), ["SubTitle"] = input.SubTitle, ["ShortTitle"] = input.ShortTitle,
            ["Description"] = input.Description, ["TnC"] = input.TnC, ["Status"] = input.Status,
            ["CreateDate"] = input.StartDate!.Value.ToString("s"), ["ExpireDate"] = input.EndDate!.Value.ToString("s")
        };
        string endpoint;
        if (kind == "rewards")
        {
            fields[input.Id == 0 ? "FullName" : "Name"] = input.Name;
            fields["Id"] = input.Id.ToString();
            fields["Point"] = input.Point!.Value.ToString(CultureInfo.InvariantCulture);
            endpoint = "api/ManageRewards/" + (input.Id == 0 ? "CreateReward" : "EditReward");
        }
        else if (kind == "vouchers")
        {
            fields["VoucherID"] = input.Id.ToString(); fields["VoucherName"] = input.Name;
            fields["CreatedDate"] = fields["CreateDate"]; fields["ExpiredDate"] = fields["ExpireDate"];
            fields["VoucherStatus"] = input.Status; fields["VoucherRules"] = JsonSerializer.Serialize(rules);
            endpoint = "api/ManageRewards/" + (input.Id == 0 ? "CreateVoucher" : "EditVoucher");
        }
        else
        {
            fields["HighlightID"] = input.Id.ToString();
            endpoint = "api/ManageHighlight/" + (input.Id == 0 ? "CreateHighlight" : "EditHighlight");
        }
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields) if (value is not null) form.Add(new StringContent(value), key);
        if (input.File is { Length: > 0 } file)
        {
            var stream = new StreamContent(file.OpenReadStream());
            stream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            form.Add(stream, "file", Path.GetFileName(file.FileName));
        }
        return await Execute(endpoint, HttpMethod.Post, form, true);
    }

    [HttpPost]
    public async Task<IActionResult> SaveOutlet(OutletInput input) => !ModelState.IsValid ? InvalidInput()
        : await Execute("api/Outlet/PostOutlet", HttpMethod.Post, JsonContent.Create(new { outletName = input.OutletName.Trim() }), true);

    [HttpPost]
    public async Task<IActionResult> SaveStaff(StaffInput input) => !ModelState.IsValid ? InvalidInput()
        : await Execute("api/OutletStaff/" + (input.Id == 0 ? "CreateOutletStaff" : "UpdateOutletStaff"), HttpMethod.Post,
            JsonContent.Create(new { outletStaffID = input.Id, input.OutletStaffName, input.OutletID, input.Pin, input.Role }), true);

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(string kind, int id)
    {
        if (id <= 0) return BadRequest(new { message = "Choose a valid record." });
        return kind switch
        {
            "members" => await Execute("api/ManageMember/ActiveOrDeactiveMember", HttpMethod.Post, JsonContent.Create(new { memberID = id }), true),
            "outlets" => await Execute($"api/Outlet/ActiveOrDeactiveOutlet/{id}", HttpMethod.Get, mutation: true),
            _ => BadRequest(new { message = "Status cannot be changed for this record type." })
        };
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string kind, int id)
    {
        if (id <= 0) return BadRequest(new { message = "Choose a valid record." });
        var path = kind switch
        {
            "rewards" => $"api/ManageRewards/DeleteReward/{id}",
            "vouchers" => $"api/ManageRewards/DeleteVoucher/{id}",
            "highlights" => $"api/ManageHighlight/DeleteHighlight/{id}",
            "staff" => $"api/OutletStaff/DeleteOutletStaff/{id}",
            _ => null
        };
        return path is null ? BadRequest(new { message = "This record cannot be deleted." })
            : await Execute(path, kind == "staff" ? HttpMethod.Delete : HttpMethod.Get, mutation: true);
    }

    [HttpPost]
    public async Task<IActionResult> AssignPoints(AssignPointsInput input) => !ModelState.IsValid ? InvalidInput()
        : await Execute("api/ManagePoint/OutletAssignPoint", HttpMethod.Post,
            JsonContent.Create(new { input.PhoneNumber, input.OutletID, input.Pin, input.Point }), true);

    [HttpPost]
    public async Task<IActionResult> RejectPoint(RejectPointInput input)
    {
        if (!ModelState.IsValid) return InvalidInput();
        try
        {
            var record = AdminApiClient.ForBrowser(await api.SendAsync($"api/ManagePoint/GetAllPointAssignRecordByPointId/{input.Id}", token: Token, cancellationToken: HttpContext.RequestAborted));
            if (record is null) return NotFound(new { message = "Point record not found." });
            var phone = record["member"]?["phoneNumber"]?.ToString();
            if (phone?.StartsWith("+60") == true) phone = "0" + phone[3..];
            if (string.IsNullOrWhiteSpace(phone)) return BadRequest(new { message = "The point record has no member phone number." });
            await api.SendAsync("api/ManagePoint/RejectPoint", HttpMethod.Post, JsonContent.Create(new
            {
                id = record["pointRecordGUID"]?.ToString(),
                assignPoint = new { phoneNumber = phone, outletID = input.OutletID, pin = input.Pin, point = 0 }
            }), Token, HttpContext.RequestAborted);
            // The upstream RejectPoint DTO has no reason field. Keep a portal audit entry.
            logger.LogInformation("Administrator {AdminID} rejected point record {RecordID}. Reason: {Reason}",
                HttpContext.Session.GetInt32("AdminID"), input.Id, input.Reason.Replace('\r', ' ').Replace('\n', ' '));
            return Json(new { success = true, message = "Point record rejected. Reason recorded in the portal audit log." });
        }
        catch (AdminApiException ex) { return ApiError(ex); }
    }

    private void ValidateImage(IFormFile? file)
    {
        if (file is null) return;
        if (file.Length is <= 0 or > 5 * 1024 * 1024)
            ModelState.AddModelError("File", "Choose an image smaller than 5 MB.");
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()) ||
            !new[] { "image/jpeg", "image/png", "image/webp", "image/gif" }.Contains(file.ContentType))
            ModelState.AddModelError("File", "Upload a JPG, PNG, WebP or GIF image.");
    }

    private async Task<IActionResult> Execute(string path, HttpMethod method, HttpContent? content = null, bool mutation = false)
    {
        try
        {
            var result = await api.SendAsync(path, method, content, Token, HttpContext.RequestAborted);
            if (mutation) return Json(new { success = true, message = result is JsonValue ? result.ToString() : "Changes saved successfully." });
            return Json(AdminApiClient.ForBrowser(result));
        }
        catch (AdminApiException ex) { return ApiError(ex); }
    }

    private IActionResult InvalidInput() => BadRequest(new
    {
        message = "Please check the highlighted fields.",
        errors = ModelState.Where(p => p.Value?.Errors.Count > 0).ToDictionary(p => p.Key,
            p => p.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Enter a valid value." : e.ErrorMessage).ToArray())
    });

    private IActionResult ApiError(AdminApiException ex, bool clearSession = true)
    {
        if (ex.StatusCode == 401 && clearSession) ClearAdminSession();
        return StatusCode(ex.StatusCode, new { message = ex.Message });
    }

    private void ClearAdminSession()
    {
        HttpContext.Session.Remove("AdminID");
        HttpContext.Session.Remove("AdminName");
        HttpContext.Session.Remove("AdminToken");
    }
}
