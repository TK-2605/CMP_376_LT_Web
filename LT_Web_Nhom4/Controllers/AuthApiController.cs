using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IPendingRegistrationService _pendingRegistrationService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AuthApiController> _logger;

        public AuthApiController(
            IJwtTokenService jwtTokenService,
            IEmailService emailService,
            IConfiguration configuration,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IPasswordHasher<ApplicationUser> passwordHasher,
            IPendingRegistrationService pendingRegistrationService,
            IWebHostEnvironment environment,
            ILogger<AuthApiController> logger)
        {
            _jwtTokenService = jwtTokenService;
            _emailService = emailService;
            _configuration = configuration;
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _passwordHasher = passwordHasher;
            _pendingRegistrationService = pendingRegistrationService;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult Status()
        {
            var googleConfigured = HasValues("Authentication:Google:ClientId", "Authentication:Google:ClientSecret");
            var smtpConfigured = HasEmailProvider();
            var jwtConfigured = _jwtTokenService.IsConfigured;

            return Ok(new AuthFeatureStatusResponse
            {
                GoogleOAuthConfigured = googleConfigured,
                SmtpConfigured = smtpConfigured,
                JwtConfigured = jwtConfigured,
                RegistrationConfirmationReady = smtpConfigured,
                ForgotPasswordReady = smtpConfigured,
                LoginUrl = Url.Action("Login", "Account", new { area = "Identity" }) ?? "/Identity/Login/Account/Login",
                RegisterUrl = Url.Action("Register", "Account", new { area = "Identity" }) ?? "/Identity/Login/Account/Register",
                ForgotPasswordUrl = Url.Action("ForgotPassword", "Account", new { area = "Identity" }) ?? "/Identity/Login/Account/ForgotPassword",
                SwaggerUrl = "/swagger",
                Items = new[]
                {
                    new AuthFeatureStatusItem
                    {
                        Name = "Google OAuth 2.0",
                        Configured = googleConfigured,
                        Detail = googleConfigured
                            ? "Đã có ClientId và ClientSecret; nút Google sẽ xuất hiện ở trang đăng nhập."
                            : "Thiếu Authentication:Google:ClientId hoặc ClientSecret."
                    },
                    new AuthFeatureStatusItem
                    {
                        Name = "Email OTP",
                        Configured = smtpConfigured,
                        Detail = smtpConfigured
                            ? $"Đã cấu hình provider {EmailConfigurationHelper.ProviderLabel(_configuration)} để gửi OTP."
                            : EmailConfigurationHelper.GetEmailProviderProblem(_configuration) ?? "Thiếu cấu hình Resend hoặc SMTP; có thể test bằng endpoint /api/auth/test-email sau khi đăng nhập Admin."
                    },
                    new AuthFeatureStatusItem
                    {
                        Name = "Xác nhận đăng ký bằng mã hoặc link",
                        Configured = smtpConfigured,
                        Detail = "Luồng đăng ký lưu PendingRegistration và xác nhận bằng mã 6 số hoặc token trong email."
                    },
                    new AuthFeatureStatusItem
                    {
                        Name = "OTP quên mật khẩu",
                        Configured = smtpConfigured,
                        Detail = "Luồng gửi OTP và liên kết reset mật khẩu đã có trong AccountController."
                    },
                    new AuthFeatureStatusItem
                    {
                        Name = "JWT access token và refresh token",
                        Configured = jwtConfigured,
                        Detail = jwtConfigured
                            ? "JWT key hợp lệ; có thể test login, refresh, revoke và /me trên Swagger."
                            : "Jwt:Key phải tối thiểu 32 ký tự để phát token thật."
                    }
                }
            });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ApiTestOnly]
        public async Task<IActionResult> Register(RegisterApiRequest request, CancellationToken cancellationToken)
        {
            if (!HasEmailProvider() && !_environment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Email provider is required for API registration outside localhost Development."
                });
            }

            var email = request.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var fullName = request.FullName.Trim();
            var studentCode = NormalizeOptional(request.StudentCode);

            if (await _userManager.FindByEmailAsync(email) is not null
                || await _userManager.FindByNameAsync(email) is not null)
            {
                return Conflict(new { message = "Email already exists." });
            }

            if (!string.IsNullOrWhiteSpace(studentCode))
            {
                var studentCodeUsed = await _userManager.Users.AnyAsync(user => user.StudentCode == studentCode, cancellationToken)
                    || await _context.PendingRegistrations.AnyAsync(item =>
                        item.StudentCode == studentCode && item.NormalizedEmail != normalizedEmail,
                        cancellationToken);

                if (studentCodeUsed)
                {
                    return Conflict(new { message = "Student code already exists or is pending confirmation." });
                }
            }

            var pendingUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                StudentCode = studentCode,
                IsActive = true
            };

            var passwordValidationResult = await ValidatePasswordForPendingUserAsync(pendingUser, request.Password);
            if (!passwordValidationResult.Succeeded)
            {
                return IdentityValidationProblem(passwordValidationResult);
            }

            var pendingResult = await _pendingRegistrationService.CreateOrUpdateAsync(
                email,
                normalizedEmail,
                email,
                _userManager.NormalizeName(email),
                fullName,
                studentCode,
                "Student",
                _passwordHasher.HashPassword(pendingUser, request.Password),
                cancellationToken);

            var emailSent = false;
            if (HasEmailProvider())
            {
                emailSent = await SendPendingRegistrationEmailAsync(pendingResult);
            }

            if (!emailSent && !_environment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status502BadGateway, new RegisterApiResponse
                {
                    PendingConfirmation = true,
                    Email = pendingResult.PendingRegistration.Email,
                    Message = "Registration was saved, but the server could not send the confirmation email. Configure Resend on Render Free or verify SMTP credentials."
                });
            }

            return Ok(new RegisterApiResponse
            {
                PendingConfirmation = true,
                Email = pendingResult.PendingRegistration.Email,
                Message = emailSent
                    ? "Registration is pending. Check email for confirmation code."
                    : "Registration is pending. In Development, use developmentCode or developmentToken to confirm.",
                DevelopmentCode = _environment.IsDevelopment() ? pendingResult.Code : null,
                DevelopmentToken = _environment.IsDevelopment() ? pendingResult.Token : null
            });
        }

        [HttpPost("register/confirm")]
        [AllowAnonymous]
        [ApiTestOnly]
        public async Task<IActionResult> ConfirmRegistration(ConfirmRegistrationApiRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Code) && string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new { message = "Code or token is required." });
            }

            var email = request.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var validation = await _pendingRegistrationService.ValidateAsync(
                normalizedEmail,
                request.Code,
                request.Token,
                cancellationToken);

            if (validation.Status != PendingRegistrationValidationStatus.Valid
                || validation.PendingRegistration is null)
            {
                return BadRequest(new
                {
                    message = "Registration confirmation failed.",
                    status = validation.Status.ToString()
                });
            }

            var pendingRegistration = validation.PendingRegistration;
            if (await _userManager.FindByEmailAsync(pendingRegistration.Email) is not null
                || await _userManager.FindByNameAsync(pendingRegistration.UserName) is not null)
            {
                return Conflict(new { message = "Email already exists." });
            }

            if (!string.IsNullOrWhiteSpace(pendingRegistration.StudentCode)
                && await _userManager.Users.AnyAsync(user => user.StudentCode == pendingRegistration.StudentCode, cancellationToken))
            {
                return Conflict(new { message = "Student code already exists." });
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
                return IdentityValidationProblem(createResult);
            }

            await AddRoleAsync(user, pendingRegistration.RoleName);
            await _pendingRegistrationService.RemoveAsync(pendingRegistration, cancellationToken);

            return Ok(new
            {
                message = "Registration confirmed. You can login with /api/auth/login.",
                user.Id,
                user.Email,
                user.FullName
            });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [ApiTestOnly]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordApiRequest request, CancellationToken cancellationToken)
        {
            if (!HasEmailProvider() && !_environment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Email provider is required for password reset outside localhost Development."
                });
            }

            var email = request.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            string? developmentCode = null;

            if (user is not null && user.IsActive)
            {
                var code = await CreatePasswordResetOtpAsync(user, cancellationToken);
                developmentCode = _environment.IsDevelopment() ? code : null;

                if (HasEmailProvider())
                {
                    var sent = await TrySendPasswordResetOtpEmailAsync(email, code);
                    if (!sent && !_environment.IsDevelopment())
                    {
                        return StatusCode(StatusCodes.Status502BadGateway, new ForgotPasswordApiResponse
                        {
                            Message = "OTP was created, but the server could not send email. Configure Resend on Render Free or verify SMTP credentials.",
                            DevelopmentCode = null
                        });
                    }
                }
            }
            else
            {
                _logger.LogInformation("API password reset requested for a missing, inactive, or unconfirmed account: {Email}.", email);
            }

            return Ok(new ForgotPasswordApiResponse
            {
                Message = "If the email exists, an OTP was created/sent.",
                DevelopmentCode = developmentCode
            });
        }

        [HttpPost("forgot-password/confirm")]
        [AllowAnonymous]
        [ApiTestOnly]
        public async Task<IActionResult> ConfirmForgotPassword(ResetPasswordOtpApiRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive)
            {
                return BadRequest(new { message = "OTP is invalid or expired." });
            }

            var otp = await _context.EmailOtps
                .Where(item => item.UserId == user.Id
                    && item.Purpose == OtpPurpose.ForgotPassword
                    && item.UsedAt == null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (otp is null || otp.ExpiresAt <= DateTime.UtcNow || otp.AttemptCount >= 5)
            {
                return BadRequest(new { message = "OTP is invalid or expired." });
            }

            if (!VerifyOtp(request.Code, otp.CodeHash, user))
            {
                otp.AttemptCount++;
                await _context.SaveChangesAsync(cancellationToken);
                return BadRequest(new { message = "OTP is invalid." });
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
            if (!result.Succeeded)
            {
                return IdentityValidationProblem(result);
            }

            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Password reset succeeded. You can login again." });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(AuthLoginRequest request)
        {
            if (!_jwtTokenService.IsConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "JWT chưa được cấu hình trên máy chủ."
                });
            }

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
            if (!result.Succeeded)
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
            }

            var tokenPair = await _jwtTokenService.GenerateTokenPairAsync(user, GetIpAddress(), GetUserAgent());
            return Ok(tokenPair);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(RefreshTokenRequest request)
        {
            if (!_jwtTokenService.IsConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var result = await _jwtTokenService.RefreshTokenAsync(request.RefreshToken, GetIpAddress(), GetUserAgent());
            return result is null ? Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn." }) : Ok(result);
        }

        [HttpPost("revoke")]
        [AllowAnonymous]
        public async Task<IActionResult> Revoke(RefreshTokenRequest request)
        {
            var revoked = await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
            return revoked ? NoContent() : NotFound();
        }

        [HttpPost("test-email")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> SendTestEmail(SendTestEmailRequest request)
        {
            if (!HasEmailProvider())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new SendTestEmailResponse
                {
                    Sent = false,
                    Message = EmailConfigurationHelper.GetEmailProviderProblem(_configuration) ?? "Chưa cấu hình đủ Resend hoặc SMTP để gửi email."
                });
            }

            var subject = string.IsNullOrWhiteSpace(request.Subject)
                ? "QuizHub SMTP test"
                : request.Subject.Trim();
            try
            {
                await _emailService.SendEmailAsync(
                request.ToEmail.Trim(),
                subject,
                "<p>Đây là email kiểm thử Gmail SMTP từ QuizHub.</p><p>Nếu bạn nhận được email này, MailKit/MimeKit đã hoạt động.</p>");

            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new SendTestEmailResponse
                {
                    Sent = false,
                    Message = $"SMTP failed: {exception.GetType().Name}: {exception.Message}"
                });
            }

            return Ok(new SendTestEmailResponse
            {
                Sent = true,
                Message = "Đã gửi email test. Vui lòng kiểm tra hộp thư nhận."
            });
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null || !user.IsActive)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                user.Id,
                user.Email,
                user.FullName,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }

        private async Task<bool> SendPendingRegistrationEmailAsync(PendingRegistrationCreateResult pendingResult)
        {
            try
            {
                var confirmationUrl = Url.Action(
                    nameof(ConfirmRegistration),
                    "AuthApi",
                    new
                    {
                        email = pendingResult.PendingRegistration.Email,
                        token = pendingResult.Token
                    },
                    Request.Scheme);

                await _emailService.SendEmailAsync(
                    pendingResult.PendingRegistration.Email,
                    "QuizHub registration confirmation",
                    $"""
                    <p>Your QuizHub confirmation code is: <strong>{pendingResult.Code}</strong></p>
                    <p>The code is valid for 15 minutes.</p>
                    <p>Swagger confirm endpoint: <code>/api/auth/register/confirm</code></p>
                    <p>Optional confirmation URL: <a href="{confirmationUrl}">{confirmationUrl}</a></p>
                    """);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not send API registration confirmation email to {Email}.", pendingResult.PendingRegistration.Email);
                return false;
            }
        }

        private async Task<bool> TrySendPasswordResetOtpEmailAsync(string email, string code)
        {
            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "QuizHub password reset OTP",
                    $"""
                    <p>Your password reset OTP is: <strong>{code}</strong></p>
                    <p>The code is valid for 5 minutes and can be tried up to 5 times.</p>
                    <p>Swagger confirm endpoint: <code>/api/auth/forgot-password/confirm</code></p>
                    """);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not send API password reset OTP to {Email}.", email);
                return false;
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

        private async Task<string> CreatePasswordResetOtpAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var activeOtps = await _context.EmailOtps
                .Where(item => item.UserId == user.Id
                    && item.Purpose == OtpPurpose.ForgotPassword
                    && item.UsedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var activeOtp in activeOtps)
            {
                activeOtp.UsedAt = now;
            }

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _context.EmailOtps.Add(new EmailOtp
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Purpose = OtpPurpose.ForgotPassword,
                CodeHash = HashOtp(code, user),
                ExpiresAt = now.AddMinutes(5),
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
            return code;
        }

        private IActionResult IdentityValidationProblem(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

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

        private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent() => Request.Headers.UserAgent.ToString();

        private bool HasValues(params string[] keys)
        {
            return keys.All(key => !string.IsNullOrWhiteSpace(_configuration[key]));
        }

        private bool HasEmailProvider()
        {
            return EmailConfigurationHelper.HasEmailProvider(_configuration);
        }
    }
}
