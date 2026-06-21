using LT_Web_Nhom4.Models;

namespace LT_Web_Nhom4.Services.Interfaces
{
    public record JwtTokenResult(
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiresAt,
        DateTime RefreshTokenExpiresAt);

    public interface IJwtTokenService
    {
        bool IsConfigured { get; }

        Task<JwtTokenResult> GenerateTokenPairAsync(ApplicationUser user, string? ipAddress, string? userAgent);

        Task<JwtTokenResult?> RefreshTokenAsync(string refreshToken, string? ipAddress, string? userAgent);

        Task<bool> RevokeRefreshTokenAsync(string refreshToken);
    }
}
