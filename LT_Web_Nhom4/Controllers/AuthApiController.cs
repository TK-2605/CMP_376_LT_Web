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
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthApiController(
            IJwtTokenService jwtTokenService,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _jwtTokenService = jwtTokenService;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(AuthLoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng, hoặc email chưa xác nhận." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng, hoặc email chưa xác nhận." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var tokenPair = await _jwtTokenService.GenerateTokenPairAsync(user, GetIpAddress(), GetUserAgent());

            return Ok(new
            {
                tokenPair.AccessToken,
                tokenPair.RefreshToken,
                tokenPair.AccessTokenExpiresAt,
                tokenPair.RefreshTokenExpiresAt,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    roles
                }
            });
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var tokenPair = await _jwtTokenService.RefreshTokenAsync(request.RefreshToken, GetIpAddress(), GetUserAgent());
            if (tokenPair is null)
            {
                return Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn." });
            }

            return Ok(tokenPair);
        }

        [HttpPost("revoke")]
        [AllowAnonymous]
        public async Task<IActionResult> Revoke(RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var revoked = await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
            return revoked ? NoContent() : NotFound(new { message = "Không tìm thấy refresh token đang hoạt động." });
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null || !user.IsActive)
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                roles
            });
        }

        private string? GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private string? GetUserAgent()
        {
            return Request.Headers["User-Agent"].ToString();
        }
    }
}
