using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class AuthLoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthFeatureStatusResponse
    {
        public bool GoogleOAuthConfigured { get; set; }

        public bool SmtpConfigured { get; set; }

        public bool JwtConfigured { get; set; }

        public bool RegistrationConfirmationReady { get; set; }

        public bool ForgotPasswordReady { get; set; }

        public string LoginUrl { get; set; } = string.Empty;

        public string RegisterUrl { get; set; } = string.Empty;

        public string ForgotPasswordUrl { get; set; } = string.Empty;

        public string SwaggerUrl { get; set; } = string.Empty;

        public IReadOnlyList<AuthFeatureStatusItem> Items { get; set; } = Array.Empty<AuthFeatureStatusItem>();
    }

    public class AuthFeatureStatusItem
    {
        public string Name { get; set; } = string.Empty;

        public bool Configured { get; set; }

        public string Detail { get; set; } = string.Empty;
    }

    public class SendTestEmailRequest
    {
        [Required, EmailAddress]
        public string ToEmail { get; set; } = string.Empty;

        [StringLength(120)]
        public string? Subject { get; set; }
    }

    public class SendTestEmailResponse
    {
        public bool Sent { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
