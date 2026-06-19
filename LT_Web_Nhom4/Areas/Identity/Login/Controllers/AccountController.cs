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
                ModelState.AddModelError(string.Empty, "Email hoac mat khau khong dung.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tai khoan dang tam khoa. Vui long lien he quan tri vien.");
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
                ModelState.AddModelError(string.Empty, "Tai khoan yeu cau xac thuc hai buoc. Chuc nang nay se duoc bo sung sau.");
                return View(model);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tai khoan dang bi khoa tam thoi.");
                return View(model);
            }

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, "Tai khoan chua duoc xac nhan email hoac chua duoc phe duyet.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoac mat khau khong dung.");
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

                TempData["AuthMessage"] = "Dang ky thanh cong. Ban co the dang nhap bang tai khoan vua tao.";
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

            TempData["AuthMessage"] = "Neu email ton tai, he thong se gui huong dan dat lai mat khau.";
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
                TempData["AuthMessage"] = $"Dang nhap OAuth that bai: {remoteError}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                TempData["AuthMessage"] = "Khong doc duoc thong tin dang nhap tu nha cung cap OAuth.";
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
                TempData["AuthMessage"] = "Phien dang nhap OAuth da het han.";
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
                ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email nay da duoc dang ky.");
                hasError = true;
            }

            if (!string.IsNullOrWhiteSpace(studentCode)
                && await _userManager.Users.AnyAsync(user => user.StudentCode == studentCode))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.StudentCode), "Ma sinh vien nay da duoc su dung.");
                hasError = true;
            }

            return hasError;
        }

        private void AddDatabaseRegistrationError(DbUpdateException exception, string email)
        {
            _logger.LogWarning(exception, "Registration failed because user data is duplicated for {Email}.", email);
            ModelState.AddModelError(string.Empty, "Email hoac ma sinh vien da ton tai. Vui long kiem tra lai thong tin dang ky.");
        }

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }
}
