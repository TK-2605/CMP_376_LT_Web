using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

        public AuthApiController(
            IJwtTokenService jwtTokenService,
            IEmailService emailService,
            IConfiguration configuration,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _jwtTokenService = jwtTokenService;
            _emailService = emailService;
            _configuration = configuration;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult Status()
        {
            var googleConfigured = HasValues("Authentication:Google:ClientId", "Authentication:Google:ClientSecret");
            var smtpConfigured = HasValues("Smtp:Host", "Smtp:UserName", "Smtp:Password", "Smtp:FromEmail");
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
                        Name = "Gmail SMTP MailKit/MimeKit",
                        Configured = smtpConfigured,
                        Detail = smtpConfigured
                            ? "SMTP đã đủ Host/UserName/Password/FromEmail để gửi email."
                            : "Thiếu cấu hình Smtp; có thể test bằng endpoint /api/auth/test-email sau khi đăng nhập Admin."
                    },
                    new AuthFeatureStatusItem
                    {
                        Name = "Xác nhận đăng ký bằng mã hoặc link",
                        Configured = smtpConfigured,
                        Detail = "Luồng đăng ký lưu PendingRegistration và xác nhận bằng mã 6 số hoặc token trong email."
                    },
                    new AuthFeatureStatusItem
                    {
                        Name = "Quên mật khẩu qua Gmail",
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
            if (!HasValues("Smtp:Host", "Smtp:UserName", "Smtp:Password", "Smtp:FromEmail"))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new SendTestEmailResponse
                {
                    Sent = false,
                    Message = "SMTP chưa được cấu hình đủ Host/UserName/Password/FromEmail."
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

        private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent() => Request.Headers.UserAgent.ToString();

        private bool HasValues(params string[] keys)
        {
            return keys.All(key => !string.IsNullOrWhiteSpace(_configuration[key]));
        }
    }
}
