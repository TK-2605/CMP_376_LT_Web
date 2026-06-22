namespace LT_Web_Nhom4.Models
{
    public class PendingRegistration
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string NormalizedEmail { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string NormalizedUserName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string? StudentCode { get; set; }

        public string RoleName { get; set; } = "Student";

        public string PasswordHash { get; set; } = string.Empty;

        public string ConfirmationCodeHash { get; set; } = string.Empty;

        public string ConfirmationTokenHash { get; set; } = string.Empty;

        public string TokenSalt { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public int AttemptCount { get; set; }

        public DateTime? LastSentAtUtc { get; set; }
    }
}
