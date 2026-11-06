using LoyaltyAppAssignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace LoyaltyAppAssignment.Controllers
{
    public class CustomerController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Check the form
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please enter your phone number and password."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                // Information sent to LoyaltyAppAPI
                var loginData = new
                {
                    phoneNumber = model.PhoneNumber,
                    password = model.Password
                };

                // Call the API
                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/Members/MemberLogin",
                    loginData
                );

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<LoginResponseViewModel>();

                    if (result != null &&
                        !string.IsNullOrEmpty(result.SuccessToken))
                    {
                        // Remember the logged-in customer
                        HttpContext.Session.SetString(
                            "MembersToken", result.SuccessToken);

                        HttpContext.Session.SetString(
                            "MembersEmail", result.Email);

                        return Json(new
                        {
                            success = true,
                            message = "Login successful!"
                        });
                    }
                }

                return Json(new
                {
                    success = false,
                    message = "Invalid login details or unexpected API response."
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Unable to connect to the API."
                });
            }
        }


        [HttpGet]
        public IActionResult Dashboard()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    message = "Please login again."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/Member/FindMemberDetails",
                    new { }
                );

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();

                    return Unauthorized(new
                    {
                        message = "Your session has expired."
                    });
                }

                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new
                    {
                        message = "Unable to load member information."
                    });
                }

                var member = await response.Content
                    .ReadFromJsonAsync<ProfileViewModel>();

                if (member == null)
                {
                    return BadRequest(new
                    {
                        message = "Member information was not found."
                    });
                }

                return Json(new
                {
                    name = member.Name,
                    point = member.Point,
                    tier = member.Tier,
                    referralCode = member.ReferralCode
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Login");
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please check your registration details."
                });
            }

            if (model.Birthday.Date > DateTime.Today)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Birthday cannot be in the future."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                // The API expects form data
                using MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new StringContent(model.Name), "name");
                form.Add(new StringContent(model.PhoneNumber), "phoneNumber");
                form.Add(new StringContent(model.Email), "email");
                form.Add(new StringContent(model.Password), "password");
                form.Add(new StringContent(model.Birthday.ToString("yyyy-MM-dd")), "birthday");

                var response = await client.PostAsync(
                    "https://alpacaapi.xcode.com.my/api/Member/RegisterMember",
                    form
                );

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Account created successfully!"
                    });
                }

                return BadRequest(new
                {
                    success = false,
                    message = "Registration failed. Check whether the phone number or email is already registered."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the API."
                });
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateOTP(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please enter your phone number."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                var data = new
                {
                    phoneNumber = model.PhoneNumber
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/Member/GenerateOTP",
                    data
                );

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "OTP requested! Please check your email."
                    });
                }

                return Json(new
                {
                    success = false,
                    message = "Could not request an OTP. Check your phone number."
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Unable to connect to the API."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Please fill in all fields correctly."
                });
            }

            if (model.Otp.Length != 6 || !model.Otp.All(char.IsDigit))
            {
                return Json(new
                {
                    success = false,
                    message = "Please enter a valid 6-digit OTP."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                var data = new
                {
                    phoneNumber = model.PhoneNumber,
                    otp = model.Otp,
                    newPassword = model.NewPassword
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/Member/MemberResetPassword",
                    data
                );

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Password reset successfully!"
                    });
                }

                return Json(new
                {
                    success = false,
                    message = "Invalid or expired OTP, or password rejected."
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Unable to connect to the API."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Login");
            }

            try
            {
                using HttpClient client = new HttpClient();

                // Send the JWT token
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                // Request member information
                var response = await client.PostAsJsonAsync(
                 "https://alpacaapi.xcode.com.my/api/Member/FindMemberDetails",
                 new { }
                 );

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login");
                }

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = "API Error: " +
                        (int)response.StatusCode + " - " +
                        await response.Content.ReadAsStringAsync();

                    return View(new ProfileViewModel());
                }

                if (response.IsSuccessStatusCode)
                {
                    var member = await response.Content
                        .ReadFromJsonAsync<ProfileViewModel>();

                    if (member != null)
                    {
                        return View(member);
                    }
                }

                ViewBag.Error = "Unable to load your profile.";
            }
            catch (Exception)
            {
                ViewBag.Error = "Could not connect to the API.";
            }

            return View(new ProfileViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please log in again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please enter a valid name and email."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                using MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new StringContent(model.Name), "name");
                form.Add(new StringContent(model.Email), "email");

                var response = await client.PostAsync(
                    "https://alpacaapi.xcode.com.my/api/Member/MemberUpdateProfile",
                    form
                );

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Profile updated successfully!"
                    });
                }

                return BadRequest(new
                {
                    success = false,
                    message = "Profile update failed. API status: " +
                              (int)response.StatusCode
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the API."
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please log in again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please check your password information."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var data = new
                {
                    oldPassword = model.OldPassword,
                    newPassword = model.NewPassword
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/Member/ChangePassword",
                    data
                );

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Password changed successfully!"
                    });
                }

                return BadRequest(new
                {
                    success = false,
                    message = "Password change failed. Check your current password. API status: " +
                              (int)response.StatusCode
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the API."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHighlights()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return Unauthorized(new
                {
                    message = "Please log in first."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberHightlight/GetAllHighlights"
                );

                if (response.IsSuccessStatusCode)
                {
                    var highlights = await response.Content
                        .ReadFromJsonAsync<List<HighlightViewModel>>();

                    if (highlights == null)
                    {
                        highlights = new List<HighlightViewModel>();
                    }

                    // Only show active promotions
                    var activeHighlights = highlights
                        .Where(h => h.Status.Equals(
                            "Active", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    return Json(activeHighlights);
                }

                return BadRequest(new
                {
                    message = "Unable to load promotions."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }


        [HttpGet]
        public IActionResult Rewards()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetRewards()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return Unauthorized(new
                {
                    message = "Please login first."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberReward/GetAllRewards"
                );

                if (response.IsSuccessStatusCode)
                {
                    var rewards = await response.Content
                        .ReadFromJsonAsync<List<RewardViewModel>>();

                    if (rewards == null)
                    {
                        rewards = new List<RewardViewModel>();
                    }

                    // Display only valid active rewards
                    var activeRewards = rewards
                        .Where(r => r.Status == "Active"
                            && !string.IsNullOrEmpty(r.Title)
                            && !string.IsNullOrEmpty(r.RewardGUID)
                            && r.Point.HasValue)
                        .ToList();

                    return Json(activeRewards);
                }

                return BadRequest(new
                {
                    message = "Unable to load rewards."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemReward(RedeemRewardViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please fill in all the fields."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var data = new
                {
                    rewardGUID = model.RewardGUID,
                    memberPassword = model.MemberPassword,
                    outletStaffPin = model.OutletStaffPin
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberReward/RedeemReward",
                    data
                );

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();

                    return Unauthorized(new
                    {
                        success = false,
                        message = "Session expired. Please login again."
                    });
                }

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Reward redeemed successfully!"
                    });
                }

                // Read the API error message
                string error = await response.Content.ReadAsStringAsync();

                return BadRequest(new
                {
                    success = false,
                    message = "Redemption failed: " + error
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the API."
                });
            }
        }


        [HttpGet]
        public IActionResult Vouchers()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetVouchers()
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    message = "Please login first."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberVoucher/GetFilterVouchers"
                );

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();

                    return Unauthorized(new
                    {
                        message = "Session expired. Please login again."
                    });
                }

                if (response.IsSuccessStatusCode)
                {
                    var vouchers = await response.Content
                        .ReadFromJsonAsync<VoucherListViewModel>();

                    return Json(vouchers ?? new VoucherListViewModel());
                }

                return BadRequest(new
                {
                    message = "Unable to load vouchers."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemVoucher(RedeemVoucherViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please fill in all fields."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var data = new
                {
                    voucherGUID = model.VoucherGUID,
                    memberPassword = model.MemberPassword,
                    outletStaffPin = model.OutletStaffPin
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberVoucher/RedeemVoucher",
                    data
                );

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();

                    return Unauthorized(new
                    {
                        success = false,
                        message = "Session expired. Please login again."
                    });
                }

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Voucher redeemed successfully!"
                    });
                }

                string error = await response.Content.ReadAsStringAsync();

                return BadRequest(new
                {
                    success = false,
                    message = "Redemption failed: " + error
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the API."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnclaimedVouchers()
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    message = "Please login first."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberVoucher/GetMemberUnclaimedVouchers"
                );

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();

                    return Content(result, "application/json");
                }

                return StatusCode((int)response.StatusCode, new
                {
                    message = "Unable to load unclaimed vouchers."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }

        [HttpGet]
        public IActionResult CollectPoints()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPoint(AssignPointViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please enter a valid number of points."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var data = new
                {
                    point = model.Point
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberPoint/AssignPoint",
                    data
                );

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<AssignPointResponseViewModel>();

                    if (result != null && !string.IsNullOrEmpty(result.Code))
                    {
                        return Json(new
                        {
                            success = true,
                            point = result.Point,
                            code = result.Code
                        });
                    }

                    return BadRequest(new
                    {
                        success = false,
                        message = "The API did not return an approval code."
                    });
                }

                string error = await response.Content.ReadAsStringAsync();

                return BadRequest(new
                {
                    success = false,
                    message = "Unable to request points: " + error
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not complete the API request."
                });
            }
        }

        [HttpGet]
        public IActionResult Outlets()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> GetOutlets()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return Unauthorized(new
                {
                    message = "Please login first."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberOutlet/GetAllOutlets"
                );

                if (response.IsSuccessStatusCode)
                {
                    var outlets = await response.Content
                        .ReadFromJsonAsync<List<OutletViewModel>>();

                    return Json(outlets ?? new List<OutletViewModel>());
                }

                return BadRequest(new
                {
                    message = "Unable to load outlets."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePoint(ApprovePointViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please fill in all approval fields."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var data = new
                {
                    code = model.Code,
                    outletGUID = model.OutletGUID,
                    pin = model.Pin
                };

                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberPoint/ApprovePoint",
                    data
                );

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Points approved successfully!"
                    });
                }

                string error = await response.Content.ReadAsStringAsync();

                return BadRequest(new
                {
                    success = false,
                    message = "Approval failed: " + error
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the API."
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetOutletStaff(string outletGUID)
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return Unauthorized(new
                {
                    message = "Please login first."
                });
            }

            if (string.IsNullOrEmpty(outletGUID))
            {
                return BadRequest(new
                {
                    message = "Please select an outlet."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberOutletStaff/GetAllOutletStaff/"
                    + Uri.EscapeDataString(outletGUID)
                );

                if (response.IsSuccessStatusCode)
                {
                    var staff = await response.Content
                        .ReadFromJsonAsync<List<OutletStaffViewModel>>();

                    return Json(staff ?? new List<OutletStaffViewModel>());
                }

                return BadRequest(new
                {
                    message = "Unable to load outlet staff."
                });
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not connect to the API."
                });
            }
        }



        [HttpGet]
        public IActionResult History()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    message = "Please login again."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                var response = await client.GetAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberHistory/GetHistoryRecord"
                );

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();
                    return Unauthorized(new
                    {
                        message = "Session expired."
                    });
                }

                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new
                    {
                        message = "Unable to retrieve history."
                    });
                }

                var history = await response.Content
                    .ReadFromJsonAsync<List<HistoryViewModel>>();

                return Json(history ?? new List<HistoryViewModel>());
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Could not load transaction history."
                });
            }
        }

        [HttpGet]
        public IActionResult Feedback()
        {
            if (HttpContext.Session.GetString("MemberToken") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFeedback(FeedbackViewModel model)
        {
            string? token = HttpContext.Session.GetString("MemberToken");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login again."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please complete all feedback fields correctly."
                });
            }

            try
            {
                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);


                var data = new
                {
                    title = model.Title,
                    description = model.Description,
                    rating = model.Rating.ToString(),
                    category = model.Category,
                    location = model.Location
                };


                var response = await client.PostAsJsonAsync(
                    "https://alpacaapi.xcode.com.my/api/MemberFeedback/CreateFeedback",
                    data
                );

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.Unauthorized)
                {
                    HttpContext.Session.Clear();

                    return Unauthorized(new
                    {
                        success = false,
                        message = "Session expired. Please login again."
                    });
                }

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Thank you! Your feedback has been submitted."
                    });
                }


                string error = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine(
                    "Feedback API error: " + error
                );

                return BadRequest(new
                {
                    success = false,
                    message = "Feedback submission failed. API status: " +
                              (int)response.StatusCode
                });

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);

                return StatusCode(503, new
                {
                    success = false,
                    message = "Could not connect to the feedback API."
                });
            }
        }

    }
}
