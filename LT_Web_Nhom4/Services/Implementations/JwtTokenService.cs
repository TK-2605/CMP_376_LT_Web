using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LT_Web_Nhom4.Services.Implementations
{
    public class JwtTokenService : IJwtTokenService
    {
        private const int RefreshTokenDays = 7;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;

        public JwtTokenService(
            ApplicationDbContext context,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<JwtTokenResult> GenerateTokenPairAsync(ApplicationUser user, string? ipAddress, string? userAgent)
        {
            var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes());
            var accessToken = await GenerateAccessTokenAsync(user, accessTokenExpiresAt);
            var refreshToken = CreateRefreshToken();
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashRefreshToken(refreshToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
                IpAddress = ipAddress,
                UserAgent = Truncate(userAgent, 512)
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            return new JwtTokenResult(accessToken, refreshToken, accessTokenExpiresAt, refreshTokenEntity.ExpiresAt);
        }

        public async Task<JwtTokenResult?> RefreshTokenAsync(string refreshToken, string? ipAddress, string? userAgent)
        {
            var tokenHash = HashRefreshToken(refreshToken);
            var storedToken = await _context.RefreshTokens
                .Include(token => token.User)
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash);

            if (storedToken is null ||
                storedToken.RevokedAt is not null ||
                storedToken.ExpiresAt <= DateTime.UtcNow ||
                !storedToken.User.IsActive)
            {
                return null;
            }

            // Rotate refresh tokens so a captured token cannot be reused after refresh.
            storedToken.RevokedAt = DateTime.UtcNow;
            return await GenerateTokenPairAsync(storedToken.User, ipAddress, userAgent);
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            var tokenHash = HashRefreshToken(refreshToken);
            var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash);
            if (storedToken is null || storedToken.RevokedAt is not null)
            {
                return false;
            }

            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<string> GenerateAccessTokenAsync(ApplicationUser user, DateTime expiresAt)
        {
            var key = _configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            {
                throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
                new("fullName", user.FullName ?? string.Empty)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private int GetAccessTokenMinutes()
        {
            return int.TryParse(_configuration["Jwt:ExpireMinutes"], out var minutes) && minutes > 0
                ? minutes
                : 120;
        }

        private static string CreateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');
        }

        private static string HashRefreshToken(string refreshToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(bytes);
        }

        private static string? Truncate(string? value, int maxLength)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value[..Math.Min(value.Length, maxLength)];
        }
    }
}
