namespace LT_Web_Nhom4.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public ApplicationUser User { get; set; } = null!;
    }
}
