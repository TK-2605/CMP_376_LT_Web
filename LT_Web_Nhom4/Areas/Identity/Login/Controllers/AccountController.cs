using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Areas.Identity.Login.Models;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IPendingRegistrationService _pendingRegistrationService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IEmailSender emailSender,
            IPasswordHasher<ApplicationUser> passwordHasher,
            IPendingRegistrationService pendingRegistrationService,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _emailSender = emailSender;
            _passwordHasher = passwordHasher;
            _pendingRegistrationService = pendingRegistrationService;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null, bool oauthUnavailable = false)
        {
            if (oauthUnavailable)
            {
                TempData["AuthMessage"] = "Google OAuth 2.0 chưa được cấu hình trên môi trường deploy.";
                TempData["AuthMessageType"] = "danger";
            }

            return View(await BuildLoginViewModelAsync(returnUrl));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            await PopulateExternalLoginStateAsync(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
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
                ModelState.AddModelError(string.Empty, "Tài khoản chưa được xác nhận email hoặc chưa được phép đăng nhập.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Register(string? returnUrl = null)
        {
            return View(await BuildRegisterViewModelAsync(returnUrl));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            await PopulateExternalRegisterStateAsync(model);

            if (!model.SmtpConfigured)
            {
                ModelState.AddModelError(string.Empty,
                    "Dịch vụ email chưa được cấu hình. Hệ thống tạm dừng đăng ký để tránh tạo tài khoản không thể xác nhận.");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var fullName = model.FullName.Trim();
            var studentCode = NormalizeOptional(model.StudentCode);

            model.Email = email;
            model.FullName = fullName;
            model.StudentCode = studentCode;

            if (!await CanStartPendingRegistrationAsync(email, normalizedEmail, studentCode))
            {
                return View(model);
            }

            var pendingUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                StudentCode = studentCode,
                IsActive = true
            };

            var passwordValidationResult = await ValidatePasswordForPendingUserAsync(pendingUser, model.Password);
            if (!passwordValidationResult.Succeeded)
            {
                foreach (var error in passwordValidationResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            var passwordHash = _passwordHasher.HashPassword(pendingUser, model.Password);
            var pendingResult = await _pendingRegistrationService.CreateOrUpdateAsync(
                email,
                normalizedEmail,
                email,
                _userManager.NormalizeName(email),
                fullName,
                studentCode,
                "Student",
                passwordHash);

            var emailError = await SendPendingRegistrationEmailAsync(pendingResult, model.ReturnUrl);
            var emailSent = string.IsNullOrWhiteSpace(emailError);
            TempData["AuthMessage"] = emailSent
                ? $"Đăng ký thành công. Mã xác nhận đã được gửi đến {email}."
                : GetDevelopmentFallbackMessage(emailError ?? "Đăng ký đã được lưu nhưng chưa gửi được email xác nhận.", pendingResult.Code);
            TempData["AuthMessageType"] = emailSent ? "success" : "danger";

            return RedirectToAction(nameof(ConfirmRegistration), new { email, returnUrl = model.ReturnUrl });
        }

        [HttpGet]
        public IActionResult ConfirmRegistration(string email, string? token = null, string? returnUrl = null)
        {
            return View(new ConfirmRegistrationViewModel
            {
                Email = email,
                Token = token,
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmRegistration(ConfirmRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.Code) && string.IsNullOrWhiteSpace(model.Token))
            {
                ModelState.AddModelError(nameof(model.Code), "Vui lòng nhập mã xác nhận hoặc dùng liên kết xác nhận trong email.");
                return View(model);
            }

            var email = model.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var validation = await _pendingRegistrationService.ValidateAsync(normalizedEmail, model.Code, model.Token);

            if (validation.Status != PendingRegistrationValidationStatus.Valid || validation.PendingRegistration is null)
            {
                AddPendingRegistrationValidationError(validation.Status);
                return View(model);
            }

            var pendingRegistration = validation.PendingRegistration;
            if (!await CanCreateConfirmedUserAsync(pendingRegistration.Email, pendingRegistration.StudentCode))
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = pendingRegistration.UserName,
                Email = pendingRegistration.Email,
                EmailConfirmed = true,
                FullName = pendingRegistration.FullName,
                StudentCode = pendingRegistration.StudentCode,
                IsActive = true,
                PasswordHash = pendingRegistration.PasswordHash
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            await AddRoleAsync(user, pendingRegistration.RoleName);
            await _pendingRegistrationService.RemoveAsync(pendingRegistration);

            TempData["AuthMessage"] = "Xác nhận đăng ký thành công. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegistrationCode(string email, string? returnUrl = null)
        {
            if (!IsSmtpConfigured())
            {
                TempData["AuthMessage"] = "Dịch vụ email chưa được cấu hình nên chưa thể gửi lại mã xác nhận.";
                TempData["AuthMessageType"] = "danger";
                return RedirectToAction(nameof(ConfirmRegistration), new { email, returnUrl });
            }

            var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
            var resendResult = await _pendingRegistrationService.ResendAsync(email.Trim(), normalizedEmail);
            if (resendResult is not null)
            {
                var emailError = await SendPendingRegistrationEmailAsync(resendResult, returnUrl);
                var emailSent = string.IsNullOrWhiteSpace(emailError);
                if (!emailSent)
                {
                    await _pendingRegistrationService.RestoreAsync(resendResult);
                }

                TempData["AuthMessage"] = emailSent
                    ? $"Hệ thống đã gửi lại mã xác nhận đến {email.Trim()}."
                    : GetDevelopmentFallbackMessage(
                        $"{emailError ?? "Chưa gửi được email xác nhận."} Mã xác nhận cũ vẫn còn hiệu lực nếu chưa hết hạn.",
                        resendResult.Code);
                TempData["AuthMessageType"] = emailSent ? "success" : "danger";
            }
            else
            {
                TempData["AuthMessage"] = "Không tìm thấy đăng ký đang chờ xác nhận.";
                TempData["AuthMessageType"] = "danger";
            }

            return RedirectToAction(nameof(ConfirmRegistration), new { email, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            TempData["AuthMessage"] = "Bạn đã đăng xuất khỏi hệ thống.";
            return LocalRedirect(GetSafeReturnUrl(returnUrl));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel
            {
                SmtpConfigured = CanUsePasswordResetOtp(),
                EmailProviderProblem = EmailConfigurationHelper.GetEmailProviderProblem(_configuration)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            model.SmtpConfigured = CanUsePasswordResetOtp();
            model.EmailProviderProblem = EmailConfigurationHelper.GetEmailProviderProblem(_configuration);
            if (!model.SmtpConfigured)
            {
                ModelState.AddModelError(string.Empty,
                    "Dịch vụ email chưa được cấu hình nên chưa thể gửi mã đặt lại mật khẩu.");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            string? developmentMessage = null;
            if (user is not null && user.IsActive)
            {
                var otpIssue = await CreatePasswordResetOtpAsync(user);
                developmentMessage = await SendPasswordResetOtpEmailAsync(email, otpIssue.Code);
                if (!string.IsNullOrWhiteSpace(developmentMessage) && !_environment.IsDevelopment())
                {
                    await DeactivatePasswordResetOtpAsync(otpIssue.OtpId);
                    ModelState.AddModelError(string.Empty, developmentMessage);
                    return View(model);
                }
            }
            else
            {
                _logger.LogInformation("Password reset requested for a missing, inactive, or unconfirmed account: {Email}.", email);
            }

            TempData["AuthMessage"] = developmentMessage
                ?? "Nếu email tồn tại, hệ thống đã gửi mã OTP đặt lại mật khẩu.";
            TempData["AuthMessageType"] = string.IsNullOrWhiteSpace(developmentMessage) ? "success" : "danger";
            return RedirectToAction(nameof(ResetPasswordOtp), new { email });
        }

        [HttpGet]
        public IActionResult ResetPasswordOtp(string email)
        {
            return View(new ResetPasswordOtpViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordOtp(ResetPasswordOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user is null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            var otp = await _context.EmailOtps
                .Where(item => item.UserId == user.Id
                    && item.Purpose == OtpPurpose.ForgotPassword
                    && item.UsedAt == null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp is null || otp.ExpiresAt <= DateTime.UtcNow || otp.AttemptCount >= 5)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            if (!VerifyOtp(model.Code, otp.CodeHash, user))
            {
                otp.AttemptCount++;
                await _context.SaveChangesAsync();
                ModelState.AddModelError(nameof(model.Code), "Mã OTP không đúng.");
                return View(model);
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["AuthMessage"] = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user is null || !user.IsActive)
            {
                TempData["AuthMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                TempData["AuthMessageType"] = "danger";
                return RedirectToAction(nameof(Login));
            }

            string resetToken;
            try
            {
                resetToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
            }
            catch (FormatException exception)
            {
                _logger.LogWarning(exception, "Invalid password reset token format for {Email}.", model.Email);
                ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            TempData["AuthMessage"] = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
        {
            var externalLogins = await _signInManager.GetExternalAuthenticationSchemesAsync();
            if (!externalLogins.Any(scheme => string.Equals(scheme.Name, provider, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["AuthMessage"] = "Google OAuth 2.0 chưa được cấu hình. Vui lòng thêm ClientId và ClientSecret trước khi đăng nhập bằng Google.";
                TempData["AuthMessageType"] = "danger";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

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
                TempData["AuthMessageType"] = "danger";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                TempData["AuthMessage"] = "Không đọc được thông tin đăng nhập từ nhà cung cấp OAuth.";
                TempData["AuthMessageType"] = "danger";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user is not null)
                {
                    if (!user.IsActive)
                    {
                        await _signInManager.SignOutAsync();
                        TempData["AuthMessage"] = "Tài khoản đang tạm khóa. Vui lòng liên hệ quản trị viên.";
                        TempData["AuthMessageType"] = "danger";
                        return RedirectToAction(nameof(Login), new { returnUrl });
                    }

                    return await RedirectAfterLoginAsync(user, returnUrl);
                }

                return LocalRedirect(GetSafeReturnUrl(returnUrl));
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["AuthMessage"] = "Nhà cung cấp OAuth không trả về email.";
                TempData["AuthMessageType"] = "danger";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                if (!existingUser.IsActive)
                {
                    TempData["AuthMessage"] = "Tài khoản đang tạm khóa. Vui lòng liên hệ quản trị viên.";
                    TempData["AuthMessageType"] = "danger";
                    return RedirectToAction(nameof(Login), new { returnUrl });
                }

                var userLogins = await _userManager.GetLoginsAsync(existingUser);
                if (!userLogins.Any(login => login.LoginProvider == info.LoginProvider && login.ProviderKey == info.ProviderKey))
                {
                    var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                    if (!addLoginResult.Succeeded)
                    {
                        TempData["AuthMessage"] = string.Join(" ", addLoginResult.Errors.Select(error => error.Description));
                        TempData["AuthMessageType"] = "danger";
                        return RedirectToAction(nameof(Login), new { returnUrl });
                    }
                }

                if (!existingUser.EmailConfirmed)
                {
                    existingUser.EmailConfirmed = true;
                    await _userManager.UpdateAsync(existingUser);
                }

                await _signInManager.SignInAsync(existingUser, isPersistent: false);
                return await RedirectAfterLoginAsync(existingUser, returnUrl);
            }

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
                TempData["AuthMessageType"] = "danger";
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
                await AddRoleAsync(user, "Student");

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

        private async Task<string?> SendPendingRegistrationEmailAsync(PendingRegistrationCreateResult pendingResult, string? returnUrl)
        {
            var confirmationUrl = Url.Action(
                nameof(ConfirmRegistration),
                "Account",
                new
                {
                    area = "Identity",
                    email = pendingResult.PendingRegistration.Email,
                    token = pendingResult.Token,
                    returnUrl
                },
                Request.Scheme);

            var body = $"""
                <p>Mã xác nhận đăng ký của bạn là: <strong>{pendingResult.Code}</strong></p>
                <p>Mã có hiệu lực trong 15 phút.</p>
                <p><a href="{confirmationUrl}">Bấm vào đây để xác nhận đăng ký</a></p>
                <p>Nếu bạn không tạo tài khoản QuizHub, vui lòng bỏ qua email này.</p>
                """;

            try
            {
                await _emailSender.SendEmailAsync(pendingResult.PendingRegistration.Email, "Xác nhận đăng ký QuizHub", body);
                return null;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not send pending registration confirmation email to {Email}.", pendingResult.PendingRegistration.Email);
                return BuildEmailProviderFailureMessage("Đăng ký đã được lưu nhưng chưa gửi được email xác nhận.", exception);
            }
        }

        private async Task<LoginViewModel> BuildLoginViewModelAsync(string? returnUrl)
        {
            var model = new LoginViewModel { ReturnUrl = returnUrl };
            await PopulateExternalLoginStateAsync(model);
            return model;
        }

        private async Task PopulateExternalLoginStateAsync(LoginViewModel model)
        {
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            model.GoogleOAuthConfigured = model.ExternalLogins.Any(scheme =>
                string.Equals(scheme.Name, "Google", StringComparison.OrdinalIgnoreCase));
            model.ShowGoogleOAuthHint = !model.GoogleOAuthConfigured;
        }

        private async Task<RegisterViewModel> BuildRegisterViewModelAsync(string? returnUrl)
        {
            var model = new RegisterViewModel { ReturnUrl = returnUrl };
            await PopulateExternalRegisterStateAsync(model);
            return model;
        }

        private async Task PopulateExternalRegisterStateAsync(RegisterViewModel model)
        {
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            model.GoogleOAuthConfigured = model.ExternalLogins.Any(scheme =>
                string.Equals(scheme.Name, "Google", StringComparison.OrdinalIgnoreCase));
            model.ShowGoogleOAuthHint = !model.GoogleOAuthConfigured;
            model.SmtpConfigured = IsSmtpConfigured();
            model.EmailProviderProblem = EmailConfigurationHelper.GetEmailProviderProblem(_configuration);
        }

        private bool IsSmtpConfigured()
        {
            return EmailConfigurationHelper.HasEmailProvider(_configuration);
        }

        private bool CanUsePasswordResetOtp()
        {
            return IsSmtpConfigured() || _environment.IsDevelopment();
        }

        private async Task<string?> SendPasswordResetEmailAsync(string email, string resetUrl)
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    email,
                    "Đặt lại mật khẩu QuizHub",
                    $"""
                    <p>Bạn đã yêu cầu đặt lại mật khẩu.</p>
                    <p><a href="{resetUrl}">Bấm vào đây để đặt lại mật khẩu</a></p>
                    <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
                    """);
                return null;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not send password reset email to {Email}.", email);
                if (_environment.IsDevelopment())
                {
                    return $"Email provider chưa gửi được email. Liên kết đặt lại mật khẩu dev: {resetUrl}";
                }

                return null;
            }
        }

        private async Task<IdentityResult> ValidatePasswordForPendingUserAsync(ApplicationUser user, string password)
        {
            var errors = new List<IdentityError>();
            foreach (var validator in _userManager.PasswordValidators)
            {
                var result = await validator.ValidateAsync(_userManager, user, password);
                if (!result.Succeeded)
                {
                    errors.AddRange(result.Errors);
                }
            }

            return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
        }

        private async Task<bool> CanStartPendingRegistrationAsync(string email, string normalizedEmail, string? studentCode)
        {
            var existingUser = await _userManager.FindByEmailAsync(email)
                ?? await _userManager.FindByNameAsync(email);

            if (existingUser is not null)
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email này đã được đăng ký.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(studentCode))
            {
                var studentCodeUsed = await _userManager.Users.AnyAsync(user => user.StudentCode == studentCode)
                    || await _context.PendingRegistrations.AnyAsync(item =>
                        item.StudentCode == studentCode && item.NormalizedEmail != normalizedEmail);

                if (studentCodeUsed)
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.StudentCode), "Mã sinh viên này đã được sử dụng hoặc đang chờ xác nhận.");
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> CanCreateConfirmedUserAsync(string email, string? studentCode)
        {
            if (await _userManager.FindByEmailAsync(email) is not null || await _userManager.FindByNameAsync(email) is not null)
            {
                ModelState.AddModelError(string.Empty, "Email này đã được đăng ký.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(studentCode)
                && await _userManager.Users.AnyAsync(user => user.StudentCode == studentCode))
            {
                ModelState.AddModelError(string.Empty, "Mã sinh viên này đã được sử dụng.");
                return false;
            }

            return true;
        }

        private void AddPendingRegistrationValidationError(PendingRegistrationValidationStatus status)
        {
            var message = status switch
            {
                PendingRegistrationValidationStatus.Expired => "Mã xác nhận đã hết hạn. Vui lòng gửi lại mã hoặc đăng ký lại.",
                PendingRegistrationValidationStatus.TooManyAttempts => "Bạn đã nhập sai quá số lần cho phép. Vui lòng đăng ký lại.",
                PendingRegistrationValidationStatus.InvalidCodeOrToken => "Mã xác nhận không đúng. Nếu bạn đã bấm gửi lại mã, hãy dùng mã mới nhất trong email.",
                _ => "Không tìm thấy đăng ký đang chờ xác nhận."
            };

            ModelState.AddModelError(string.Empty, message);
        }

        private async Task AddRoleAsync(ApplicationUser user, string roleName)
        {
            var safeRoleName = string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "Student";

            if (!await _roleManager.RoleExistsAsync(safeRoleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(safeRoleName));
            }

            if (!await _userManager.IsInRoleAsync(user, safeRoleName))
            {
                await _userManager.AddToRoleAsync(user, safeRoleName);
            }
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
            TempData["AuthMessage"] = $"Đăng nhập thành công. Xin chào {GetDisplayName(user)}.";
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

        private string GetDevelopmentFallbackMessage(string message, string code)
        {
            if (_environment.IsDevelopment())
            {
                return $"{message} Mã xác nhận dev: {code}";
            }

            var provider = EmailConfigurationHelper.ProviderLabel(_configuration);
            var problem = EmailConfigurationHelper.GetEmailProviderProblem(_configuration)
                ?? "Vui lòng kiểm tra Render logs hoặc thử gửi lại mã sau.";
            if (message.Contains("Provider hiện tại:", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            return $"{message} Provider hiện tại: {provider}. {problem}";
        }

        private async Task<PasswordResetOtpIssue> CreatePasswordResetOtpAsync(ApplicationUser user)
        {
            var now = DateTime.UtcNow;
            var activeOtps = await _context.EmailOtps
                .Where(item => item.UserId == user.Id
                    && item.Purpose == OtpPurpose.ForgotPassword
                    && item.UsedAt == null)
                .ToListAsync();
            foreach (var activeOtp in activeOtps)
            {
                activeOtp.UsedAt = now;
            }

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var otp = new EmailOtp
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Purpose = OtpPurpose.ForgotPassword,
                CodeHash = HashOtp(code, user),
                ExpiresAt = now.AddMinutes(5),
                CreatedAt = now
            };
            _context.EmailOtps.Add(otp);
            await _context.SaveChangesAsync();
            return new PasswordResetOtpIssue(code, otp.Id);
        }

        private async Task DeactivatePasswordResetOtpAsync(int otpId)
        {
            var otp = await _context.EmailOtps.FirstOrDefaultAsync(item => item.Id == otpId);
            if (otp is null || otp.UsedAt is not null)
            {
                return;
            }

            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task<string?> SendPasswordResetOtpEmailAsync(string email, string code)
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    email,
                    "Mã OTP đặt lại mật khẩu QuizHub",
                    $"<p>Mã OTP đặt lại mật khẩu của bạn là: <strong>{code}</strong></p><p>Mã có hiệu lực trong 5 phút và chỉ được nhập tối đa 5 lần.</p>");
                return null;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not send password reset OTP to {Email}.", email);
                if (_environment.IsDevelopment())
                {
                    return $"Email provider chưa gửi được OTP. Mã OTP phát triển: {code}";
                }

                return BuildEmailProviderFailureMessage("Chưa gửi được OTP qua email. OTP vừa tạo đã được vô hiệu hóa.", exception);
            }
        }

        private string BuildEmailProviderFailureMessage(string prefix, Exception? exception)
        {
            var provider = EmailConfigurationHelper.ProviderLabel(_configuration);
            var providerProblem = EmailConfigurationHelper.GetEmailProviderProblem(_configuration);
            var problemText = string.IsNullOrWhiteSpace(providerProblem)
                ? "Kiểm tra Render logs để xem phản hồi chi tiết từ provider."
                : providerProblem;
            var exceptionText = exception is null
                ? string.Empty
                : $" Lỗi: {exception.GetType().Name}: {exception.Message}";

            return $"{prefix} Provider hiện tại: {provider}. {problemText}{exceptionText}";
        }

        private sealed record PasswordResetOtpIssue(string Code, int OtpId);

        private static string HashOtp(string code, ApplicationUser user)
        {
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{code}:{user.Id}:{user.SecurityStamp}")));
        }

        private static bool VerifyOtp(string code, string expectedHash, ApplicationUser user)
        {
            var actual = Convert.FromHexString(HashOtp(code, user));
            var expected = Convert.FromHexString(expectedHash);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static string GetDisplayName(ApplicationUser user)
        {
            return string.IsNullOrWhiteSpace(user.FullName)
                ? user.Email ?? user.UserName ?? "bạn"
                : user.FullName;
        }
    }
}
