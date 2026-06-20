using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Areas.Identity.Login.Models;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
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
        private readonly IEmailService _emailService;
        private readonly IEmailSender _emailSender;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IPendingRegistrationService _pendingRegistrationService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IEmailService emailService,
            IEmailSender emailSender,
            IPasswordHasher<ApplicationUser> passwordHasher,
            IPendingRegistrationService pendingRegistrationService,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _emailService = emailService;
            _emailSender = emailSender;
            _passwordHasher = passwordHasher;
            _pendingRegistrationService = pendingRegistrationService;
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

            if (!await PrepareExistingUserForRegistrationAsync(email, studentCode))
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
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var normalizedUserName = _userManager.NormalizeName(email);
            var pendingResult = await _pendingRegistrationService.CreateOrUpdateAsync(
                email,
                normalizedEmail,
                email,
                normalizedUserName,
                fullName,
                studentCode,
                "Student",
                passwordHash);

            await SendPendingRegistrationEmailAsync(pendingResult, model.ReturnUrl);

            TempData["AuthMessage"] = "Dang ky thanh cong. Vui long kiem tra email de lay ma xac nhan.";
            return RedirectToAction(nameof(ConfirmRegistration), new { email, returnUrl = model.ReturnUrl });

            /*
            if (result.Succeeded)
            {
                await AddDefaultStudentRoleAsync(user);
                await CreateAndSendOtpAsync(user, OtpPurpose.ConfirmEmail);

                TempData["AuthMessage"] = "Đăng ký thành công. Vui lòng kiểm tra email để lấy mã xác nhận.";
                return RedirectToAction(nameof(ConfirmEmailOtp), new { email, returnUrl = model.ReturnUrl });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
            */
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect(GetSafeReturnUrl(returnUrl));
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
                ModelState.AddModelError(nameof(model.Code), "Vui long nhap ma xac nhan hoac dung lien ket xac nhan trong email.");
                return View(model);
            }

            var email = model.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var validation = await _pendingRegistrationService.ValidateAsync(
                email,
                normalizedEmail,
                model.Code,
                model.Token);

            if (validation.Status != PendingRegistrationValidationStatus.Valid || validation.PendingRegistration is null)
            {
                AddPendingRegistrationValidationError(validation.Status);
                return View(model);
            }

            var pendingRegistration = validation.PendingRegistration;
            if (!await PrepareExistingUserForRegistrationAsync(pendingRegistration.Email, pendingRegistration.StudentCode))
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

            TempData["AuthMessage"] = "Xac nhan dang ky thanh cong. Ban co the dang nhap.";
            return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegistrationCode(string email, string? returnUrl = null)
        {
            var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
            var resendResult = await _pendingRegistrationService.ResendAsync(email.Trim(), normalizedEmail);
            if (resendResult is not null)
            {
                await SendPendingRegistrationEmailAsync(resendResult, returnUrl);
            }

            TempData["AuthMessage"] = "Neu dang ky dang cho ton tai, he thong da gui lai ma xac nhan.";
            return RedirectToAction(nameof(ConfirmRegistration), new { email, returnUrl });
        }

        [HttpGet]
        public IActionResult ConfirmEmailOtp(string email, string? returnUrl = null)
        {
            return View(new ConfirmEmailOtpViewModel { Email = email, ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmailOtp(ConfirmEmailOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
                return View(model);
            }

            var otp = await FindLatestOtpAsync(user, OtpPurpose.ConfirmEmail);
            if (otp is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy mã OTP hợp lệ.");
                return View(model);
            }

            if (!await ValidateOtpAsync(user, otp, model.Code))
            {
                return View(model);
            }

            user.EmailConfirmed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            TempData["AuthMessage"] = "Xác nhận email thành công. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation(string email, string? returnUrl = null)
        {
            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user is not null && !await _userManager.IsEmailConfirmedAsync(user))
            {
                await CreateAndSendOtpAsync(user, OtpPurpose.ConfirmEmail);
            }

            TempData["AuthMessage"] = "Nếu email tồn tại và chưa xác nhận, hệ thống đã gửi lại mã OTP.";
            return RedirectToAction(nameof(ConfirmEmailOtp), new { email, returnUrl });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user is not null && user.IsActive && await _userManager.IsEmailConfirmedAsync(user))
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
                var resetUrl = Url.Action(
                    nameof(ResetPassword),
                    "Account",
                    new { area = "Identity", email, token = encodedToken },
                    Request.Scheme);

                if (!string.IsNullOrWhiteSpace(resetUrl))
                {
                    try
                    {
                        await _emailSender.SendEmailAsync(
                            email,
                            "Dat lai mat khau",
                            $"""
                            <p>Ban da yeu cau dat lai mat khau.</p>
                            <p><a href="{resetUrl}">Bam vao day de dat lai mat khau</a></p>
                            <p>Neu ban khong yeu cau, vui long bo qua email nay.</p>
                            """);
                        _logger.LogInformation("Password reset email was sent to {Email}.", email);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Could not send password reset email to {Email}.", email);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not build password reset URL for {Email}.", email);
                }
            }
            else
            {
                _logger.LogInformation("Password reset requested for a missing or inactive account: {Email}.", email);
            }

            TempData["AuthMessage"] = "Neu email ton tai, he thong da gui lien ket dat lai mat khau.";
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
                TempData["AuthMessage"] = "Lien ket dat lai mat khau khong hop le.";
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
                ModelState.AddModelError(string.Empty, "Lien ket dat lai mat khau khong hop le.");
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

            TempData["AuthMessage"] = "Dat lai mat khau thanh cong. Ban co the dang nhap.";
            return RedirectToAction(nameof(Login));
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
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản hợp lệ.");
                return View(model);
            }

            var otp = await FindLatestOtpAsync(user, OtpPurpose.ForgotPassword);
            if (otp is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy mã OTP hợp lệ.");
                return View(model);
            }

            if (!await ValidateOtpAsync(user, otp, model.Code))
            {
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
                    if (!user.IsActive)
                    {
                        await _signInManager.SignOutAsync();
                        TempData["AuthMessage"] = "Tài khoản đang tạm khóa. Vui lòng liên hệ quản trị viên.";
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
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                if (!existingUser.IsActive)
                {
                    TempData["AuthMessage"] = "Tài khoản đang tạm khóa. Vui lòng liên hệ quản trị viên.";
                    return RedirectToAction(nameof(Login), new { returnUrl });
                }

                var userLogins = await _userManager.GetLoginsAsync(existingUser);
                if (!userLogins.Any(login => login.LoginProvider == info.LoginProvider && login.ProviderKey == info.ProviderKey))
                {
                    var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                    if (!addLoginResult.Succeeded)
                    {
                        TempData["AuthMessage"] = string.Join(" ", addLoginResult.Errors.Select(error => error.Description));
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

        private async Task SendPendingRegistrationEmailAsync(PendingRegistrationCreateResult pendingResult, string? returnUrl)
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
                <p>Ma xac nhan dang ky cua ban la: <strong>{pendingResult.Code}</strong></p>
                <p>Ma co hieu luc trong 15 phut.</p>
                <p><a href="{confirmationUrl}">Bam vao day de xac nhan dang ky</a></p>
                """;

            try
            {
                await _emailSender.SendEmailAsync(pendingResult.PendingRegistration.Email, "Xac nhan dang ky", body);
                _logger.LogInformation("Pending registration confirmation email sent to {Email}.", pendingResult.PendingRegistration.Email);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not send pending registration confirmation email to {Email}.", pendingResult.PendingRegistration.Email);
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

        private async Task<bool> PrepareExistingUserForRegistrationAsync(string email, string? studentCode)
        {
            var existingUser = await _userManager.FindByEmailAsync(email)
                ?? await _userManager.FindByNameAsync(email);

            if (existingUser is not null)
            {
                if (await _userManager.IsEmailConfirmedAsync(existingUser))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email nay da duoc dang ky.");
                    return false;
                }

                if (!await DeleteUnconfirmedUserIfSafeAsync(existingUser))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email nay dang co tai khoan chua xac nhan va khong the tu dong thay the.");
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(studentCode)
                && await _userManager.Users.AnyAsync(user => user.StudentCode == studentCode && user.Email != email))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.StudentCode), "Ma sinh vien nay da duoc su dung.");
                return false;
            }

            return true;
        }

        private async Task<bool> DeleteUnconfirmedUserIfSafeAsync(ApplicationUser user)
        {
            var hasImportantData =
                await _context.Classes.AnyAsync(item => item.TeacherId == user.Id)
                || await _context.ClassMembers.AnyAsync(item => item.UserId == user.Id)
                || await _context.Questions.AnyAsync(item => item.CreatedById == user.Id)
                || await _context.Exams.AnyAsync(item => item.CreatedById == user.Id)
                || await _context.ExamAttempts.AnyAsync(item => item.UserId == user.Id);

            if (hasImportantData)
            {
                _logger.LogWarning("Unconfirmed user {Email} was not deleted because dependent data exists.", user.Email);
                return false;
            }

            _context.EmailOtps.RemoveRange(_context.EmailOtps.Where(otp => otp.UserId == user.Id));
            _context.RefreshTokens.RemoveRange(_context.RefreshTokens.Where(token => token.UserId == user.Id));
            _context.UserLogins.RemoveRange(_context.UserLogins.Where(login => login.UserId == user.Id));
            _context.UserClaims.RemoveRange(_context.UserClaims.Where(claim => claim.UserId == user.Id));
            _context.UserTokens.RemoveRange(_context.UserTokens.Where(token => token.UserId == user.Id));
            _context.UserRoles.RemoveRange(_context.UserRoles.Where(role => role.UserId == user.Id));

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Could not delete unconfirmed user {Email}: {Errors}", user.Email, string.Join(" ", result.Errors.Select(error => error.Description)));
            }

            return result.Succeeded;
        }

        private void AddPendingRegistrationValidationError(PendingRegistrationValidationStatus status)
        {
            var message = status switch
            {
                PendingRegistrationValidationStatus.Expired => "Ma xac nhan da het han. Vui long dang ky lai hoac gui lai ma neu con phien cho.",
                PendingRegistrationValidationStatus.TooManyAttempts => "Ban da nhap sai qua so lan cho phep. Vui long dang ky lai.",
                PendingRegistrationValidationStatus.InvalidCodeOrToken => "Ma xac nhan khong dung.",
                _ => "Khong tim thay dang ky dang cho."
            };

            ModelState.AddModelError(string.Empty, message);
        }

        private async Task AddRoleAsync(ApplicationUser user, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return;
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }
        }

        private async Task CreateAndSendOtpAsync(ApplicationUser user, OtpPurpose purpose)
        {
            var now = DateTime.UtcNow;
            var existingOtps = await _context.EmailOtps
                .Where(otp => otp.UserId == user.Id && otp.Purpose == purpose && otp.UsedAt == null)
                .ToListAsync();

            foreach (var existingOtp in existingOtps)
            {
                existingOtp.UsedAt = now;
            }

            var code = GenerateOtp();
            var emailOtp = new EmailOtp
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Purpose = purpose,
                CodeHash = HashOtp(code, user),
                ExpiresAt = now.AddMinutes(5),
                CreatedAt = now
            };

            _context.EmailOtps.Add(emailOtp);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendOtpEmailAsync(emailOtp.Email, code, purpose.ToString());
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not send OTP email for {Email} and purpose {Purpose}.", emailOtp.Email, purpose);
            }
        }

        private Task<EmailOtp?> FindLatestOtpAsync(ApplicationUser user, OtpPurpose purpose)
        {
            return _context.EmailOtps
                .Where(otp => otp.UserId == user.Id && otp.Purpose == purpose && otp.UsedAt == null)
                .OrderByDescending(otp => otp.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> ValidateOtpAsync(ApplicationUser user, EmailOtp otp, string code)
        {
            if (otp.ExpiresAt < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn.");
                return false;
            }

            if (otp.AttemptCount >= 5)
            {
                ModelState.AddModelError(string.Empty, "Bạn đã nhập sai OTP quá số lần cho phép.");
                return false;
            }

            if (!VerifyOtp(code, otp.CodeHash, user))
            {
                otp.AttemptCount++;
                await _context.SaveChangesAsync();
                ModelState.AddModelError(string.Empty, "Mã OTP không đúng.");
                return false;
            }

            return true;
        }

        private static string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        private static string HashOtp(string code, ApplicationUser user)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{code}:{user.Id}:{user.SecurityStamp}"));
            return Convert.ToHexString(bytes);
        }

        private static bool VerifyOtp(string code, string codeHash, ApplicationUser user)
        {
            return string.Equals(HashOtp(code, user), codeHash, StringComparison.Ordinal);
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
