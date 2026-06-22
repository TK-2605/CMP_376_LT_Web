using Microsoft.Extensions.Configuration;

namespace LT_Web_Nhom4.Services
{
    public static class EmailConfigurationHelper
    {
        public static bool HasEmailProvider(IConfiguration configuration)
        {
            return HasResendProvider(configuration) || HasUsableSmtpProvider(configuration);
        }

        public static bool HasResendProvider(IConfiguration configuration)
        {
            return HasValue(configuration["Resend:ApiKey"])
                && HasValue(configuration["Resend:FromEmail"]);
        }

        public static bool HasSmtpProvider(IConfiguration configuration)
        {
            return HasValue(GetSmtpHost(configuration))
                && HasValue(GetSmtpUserName(configuration))
                && HasValue(GetSmtpPassword(configuration))
                && HasValue(GetSmtpFromEmail(configuration));
        }

        public static bool HasUsableSmtpProvider(IConfiguration configuration)
        {
            return HasSmtpProvider(configuration) && !IsSmtpBlockedByRuntime(configuration);
        }

        public static bool IsSmtpBlockedByRuntime(IConfiguration configuration)
        {
            if (!HasSmtpProvider(configuration))
            {
                return false;
            }

            if (IsTruthy(configuration["Email:AllowSmtpOnRender"]))
            {
                return false;
            }

            return IsRenderRuntime(configuration);
        }

        public static string ProviderLabel(IConfiguration configuration)
        {
            if (HasResendProvider(configuration))
            {
                return "Resend HTTPS";
            }

            if (IsSmtpBlockedByRuntime(configuration))
            {
                return "SMTP (blocked on Render Free)";
            }

            if (HasSmtpProvider(configuration))
            {
                return "SMTP";
            }

            return "Chưa cấu hình";
        }

        public static string? GetSmtpHost(IConfiguration configuration)
        {
            return FirstConfigured(configuration, "Smtp:Host", "EmailSettings:Host");
        }

        public static int GetSmtpPort(IConfiguration configuration)
        {
            var value = FirstConfigured(configuration, "Smtp:Port", "EmailSettings:Port");
            return int.TryParse(value, out var port) ? port : 587;
        }

        public static string? GetSmtpUserName(IConfiguration configuration)
        {
            return FirstConfigured(configuration, "Smtp:UserName", "EmailSettings:UserName", "EmailSettings:Email");
        }

        public static string? GetSmtpPassword(IConfiguration configuration)
        {
            return FirstConfigured(configuration, "Smtp:Password", "EmailSettings:Password");
        }

        public static string? GetSmtpFromEmail(IConfiguration configuration)
        {
            return FirstConfigured(configuration, "Smtp:FromEmail", "EmailSettings:FromEmail", "EmailSettings:Email");
        }

        public static string? GetSmtpFromName(IConfiguration configuration)
        {
            return FirstConfigured(configuration, "Smtp:FromName", "EmailSettings:SenderName") ?? "QuizHub";
        }

        public static string? GetSmtpSecureSocketOptions(IConfiguration configuration)
        {
            return FirstConfigured(configuration, "Smtp:SecureSocketOptions", "EmailSettings:SecureSocketOptions");
        }

        public static int GetSmtpTimeoutSeconds(IConfiguration configuration)
        {
            var value = FirstConfigured(configuration, "Smtp:TimeoutSeconds", "EmailSettings:TimeoutSeconds");
            return int.TryParse(value, out var seconds) ? Math.Clamp(seconds, 5, 60) : 20;
        }

        public static string? GetEmailProviderProblem(IConfiguration configuration)
        {
            if (IsSmtpBlockedByRuntime(configuration))
            {
                return "SMTP is configured, but Render Free blocks outbound SMTP ports. Configure Resend/Brevo/SendGrid over HTTPS or upgrade Render.";
            }

            return HasEmailProvider(configuration)
                ? null
                : "No usable email provider is configured.";
        }

        private static bool IsRenderRuntime(IConfiguration configuration)
        {
            return HasValue(configuration["RENDER"])
                || HasValue(configuration["RENDER_SERVICE_ID"])
                || HasValue(configuration["RENDER_EXTERNAL_URL"])
                || HasValue(Environment.GetEnvironmentVariable("RENDER"))
                || HasValue(Environment.GetEnvironmentVariable("RENDER_SERVICE_ID"))
                || HasValue(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
        }

        private static string? FirstConfigured(IConfiguration configuration, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = configuration[key];
                if (HasValue(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool HasValue(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsTruthy(string? value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
