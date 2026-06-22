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

    public class RegisterApiRequest
    {
        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? StudentCode { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterApiResponse
    {
        public bool PendingConfirmation { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string ConfirmEndpoint { get; set; } = "/api/auth/register/confirm";

        public string? DevelopmentCode { get; set; }

        public string? DevelopmentToken { get; set; }
    }

    public class ConfirmRegistrationApiRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(6, MinimumLength = 6)]
        public string? Code { get; set; }

        public string? Token { get; set; }
    }

    public class ForgotPasswordApiRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordApiResponse
    {
        public string Message { get; set; } = string.Empty;

        public string ConfirmEndpoint { get; set; } = "/api/auth/forgot-password/confirm";

        public string? DevelopmentCode { get; set; }
    }

    public class ResetPasswordOtpApiRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, RegularExpression("^[0-9]{6}$")]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
