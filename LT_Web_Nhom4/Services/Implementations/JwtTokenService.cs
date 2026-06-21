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
    public sealed class JwtTokenService : IJwtTokenService
    {
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

        public bool IsConfigured => GetSigningKey() is not null;

        public async Task<JwtTokenResult> GenerateTokenPairAsync(
            ApplicationUser user,
            string? ipAddress,
            string? userAgent)
        {
            var signingKey = GetSigningKey()
                ?? throw new InvalidOperationException("JWT chưa được cấu hình khóa ký tối thiểu 32 ký tự.");
            var now = DateTime.UtcNow;
            var accessTokenExpiresAt = now.AddMinutes(GetPositiveInt("Jwt:ExpireMinutes", 120));
            var refreshToken = CreateRefreshToken();
            var refreshTokenExpiresAt = now.AddDays(GetPositiveInt("Jwt:RefreshTokenDays", 7));

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

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(signingKey),
                SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                notBefore: now,
                expires: accessTokenExpiresAt,
                signingCredentials: credentials);

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashRefreshToken(refreshToken),
                CreatedAt = now,
                ExpiresAt = refreshTokenExpiresAt,
                IpAddress = Truncate(ipAddress, 100),
                UserAgent = Truncate(userAgent, 512)
            });
            await _context.SaveChangesAsync();

            return new JwtTokenResult(
                new JwtSecurityTokenHandler().WriteToken(token),
                refreshToken,
                accessTokenExpiresAt,
                refreshTokenExpiresAt);
        }

        public async Task<JwtTokenResult?> RefreshTokenAsync(
            string refreshToken,
            string? ipAddress,
            string? userAgent)
        {
            var tokenHash = HashRefreshToken(refreshToken);
            var storedToken = await _context.RefreshTokens
                .Include(token => token.User)
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash);

            if (storedToken is null
                || storedToken.RevokedAt is not null
                || storedToken.ExpiresAt <= DateTime.UtcNow
                || !storedToken.User.IsActive)
            {
                return null;
            }

            storedToken.RevokedAt = DateTime.UtcNow;
            return await GenerateTokenPairAsync(storedToken.User, ipAddress, userAgent);
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            var tokenHash = HashRefreshToken(refreshToken);
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash);
            if (storedToken is null || storedToken.RevokedAt is not null)
            {
                return false;
            }

            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private byte[]? GetSigningKey()
        {
            var key = _configuration["Jwt:Key"];
            return string.IsNullOrWhiteSpace(key) || key.Length < 32
                ? null
                : Encoding.UTF8.GetBytes(key);
        }

        private int GetPositiveInt(string key, int fallback)
        {
            return int.TryParse(_configuration[key], out var value) && value > 0 ? value : fallback;
        }

        private static string CreateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');
        }

        private static string HashRefreshToken(string refreshToken)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }

        private static string? Truncate(string? value, int maxLength)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, maxLength)];
        }
    }
}
