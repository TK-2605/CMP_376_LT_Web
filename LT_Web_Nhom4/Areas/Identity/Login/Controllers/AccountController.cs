using System.Security.Claims;
using LT_Web_Nhom4.Areas.Identity.Login.Models;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Identity.Login.Controllers
{
    [Area("Identity")]
    [AllowAnonymous]
    [Route("Identity/Login/[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            return View(new LoginViewModel
            {
                ReturnUrl = returnUrl,
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đang tạm khóa. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                return await RedirectAfterLoginAsync(user, model.ReturnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản yêu cầu xác thực hai bước.");
                return View(model);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đang bị khóa tạm thời.");
                return View(model);
            }

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản chưa được phép đăng nhập.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            return View(new RegisterViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var fullName = model.FullName.Trim();
            var studentCode = NormalizeOptional(model.StudentCode);

            model.Email = email;
            model.FullName = fullName;
            model.StudentCode = studentCode;

            if (await AddDuplicateUserErrorsAsync(email, studentCode))
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                StudentCode = studentCode,
                IsActive = true
            };

            IdentityResult result;
            try
            {
                result = await _userManager.CreateAsync(user, model.Password);
            }
            catch (DbUpdateException exception)
            {
                AddDatabaseRegistrationError(exception, email);
                return View(model);
            }

            if (result.Succeeded)
            {
                await AddDefaultStudentRoleAsync(user);

                TempData["AuthMessage"] = "Đăng ký thành công. Bạn có thể đăng nhập bằng tài khoản vừa tạo.";
                return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect(GetSafeReturnUrl(returnUrl));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            TempData["AuthMessage"] = "Chức năng đặt lại mật khẩu qua Gmail chưa được kích hoạt.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { area = "Identity", returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError is not null)
            {
                TempData["AuthMessage"] = $"Đăng nhập OAuth thất bại: {remoteError}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                TempData["AuthMessage"] = "Không đọc được thông tin đăng nhập từ nhà cung cấp OAuth.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user is not null)
                {
                    return await RedirectAfterLoginAsync(user, returnUrl);
                }

                return LocalRedirect(GetSafeReturnUrl(returnUrl));
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

            return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel
            {
                Email = email,
                FullName = fullName,
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var fullName = model.FullName.Trim();
            var studentCode = NormalizeOptional(model.StudentCode);

            model.Email = email;
            model.FullName = fullName;
            model.StudentCode = studentCode;

            if (await AddDuplicateUserErrorsAsync(email, studentCode))
            {
                return View(model);
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                TempData["AuthMessage"] = "Phiên đăng nhập OAuth đã hết hạn.";
                return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                StudentCode = studentCode,
                IsActive = true
            };

            IdentityResult createResult;
            try
            {
                createResult = await _userManager.CreateAsync(user);
            }
            catch (DbUpdateException exception)
            {
                AddDatabaseRegistrationError(exception, email);
                return View(model);
            }

            if (createResult.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await AddDefaultStudentRoleAsync(user);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return await RedirectAfterLoginAsync(user, model.ReturnUrl);
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private string GetSafeReturnUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return Url.Action("Index", "Home", new { area = "" }) ?? "/";
        }

        private async Task<IActionResult> RedirectAfterLoginAsync(ApplicationUser user, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            return RedirectToAction("Index", "Home", new { area = "" });
        }

        private async Task AddDefaultStudentRoleAsync(ApplicationUser user)
        {
            if (await _roleManager.RoleExistsAsync("Student") && await _userManager.IsInRoleAsync(user, "Student") == false)
            {
                await _userManager.AddToRoleAsync(user, "Student");
            }
        }

        private async Task<bool> AddDuplicateUserErrorsAsync(string email, string? studentCode)
        {
            var hasError = false;

            if (await _userManager.FindByEmailAsync(email) is not null || await _userManager.FindByNameAsync(email) is not null)
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email này đã được đăng ký.");
                hasError = true;
            }

            if (!string.IsNullOrWhiteSpace(studentCode)
                && await _userManager.Users.AnyAsync(user => user.StudentCode == studentCode))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.StudentCode), "Mã sinh viên này đã được sử dụng.");
                hasError = true;
            }

            return hasError;
        }

        private void AddDatabaseRegistrationError(DbUpdateException exception, string email)
        {
            _logger.LogWarning(exception, "Registration failed because user data is duplicated for {Email}.", email);
            ModelState.AddModelError(string.Empty, "Email hoặc mã sinh viên đã tồn tại. Vui lòng kiểm tra lại thông tin đăng ký.");
        }

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }
}
