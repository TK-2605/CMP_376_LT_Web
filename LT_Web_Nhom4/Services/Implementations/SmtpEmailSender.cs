using LT_Web_Nhom4.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LT_Web_Nhom4.Services.Implementations
{
    public class SmtpEmailSender : IEmailSender, IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (IsResendConfigured())
            {
                await SendWithResendAsync(email, subject, htmlMessage);
                return;
            }

            await SendWithSmtpAsync(email, subject, htmlMessage);
        }

        private async Task SendWithSmtpAsync(string email, string subject, string htmlMessage)
        {
            var host = _configuration["Smtp:Host"];
            var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
            var userName = _configuration["Smtp:UserName"];
            var password = _configuration["Smtp:Password"];
            var fromEmail = _configuration["Smtp:FromEmail"];
            var fromName = _configuration["Smtp:FromName"] ?? "QuizHub";
            var timeoutSeconds = int.TryParse(_configuration["Smtp:TimeoutSeconds"], out var configuredTimeout)
                ? Math.Clamp(configuredTimeout, 5, 60)
                : 20;
            var secureSocketOptions = ResolveSecureSocketOptions(_configuration["Smtp:SecureSocketOptions"], port);

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("SMTP configuration is incomplete. Email '{Subject}' was not sent to {Email}.", subject, email);
                throw new InvalidOperationException("SMTP chưa được cấu hình đầy đủ.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage };

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var client = new SmtpClient
            {
                Timeout = timeoutSeconds * 1000
            };

            await client.ConnectAsync(host, port, secureSocketOptions, timeout.Token);
            await client.AuthenticateAsync(userName, password, timeout.Token);
            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);

            _logger.LogInformation("SMTP email '{Subject}' sent to {Email}.", subject, email);
        }

        private async Task SendWithResendAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _configuration["Resend:ApiKey"];
            var fromEmail = _configuration["Resend:FromEmail"];
            var fromName = _configuration["Resend:FromName"] ?? _configuration["Smtp:FromName"] ?? "QuizHub";
            var timeoutSeconds = int.TryParse(_configuration["Resend:TimeoutSeconds"], out var configuredTimeout)
                ? Math.Clamp(configuredTimeout, 5, 60)
                : 20;

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("Resend email provider is not configured.");
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await httpClient.PostAsJsonAsync(
                "https://api.resend.com/emails",
                new
                {
                    from = $"{fromName} <{fromEmail}>",
                    to = new[] { email },
                    subject,
                    html = htmlMessage
                },
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                throw new InvalidOperationException($"Resend returned HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("Resend email '{Subject}' sent to {Email}.", subject, email);
        }

        public Task SendOtpEmailAsync(string toEmail, string otp, string purpose)
        {
            var isConfirmEmail = string.Equals(purpose, "ConfirmEmail", StringComparison.OrdinalIgnoreCase);
            var subject = isConfirmEmail ? "Mã xác nhận email" : "Mã đặt lại mật khẩu";
            var htmlBody = $"""
                <p>Mã OTP của bạn là: <strong>{otp}</strong></p>
                <p>Mã có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này.</p>
                """;

            return SendEmailAsync(toEmail, subject, htmlBody);
        }

        private static SecureSocketOptions ResolveSecureSocketOptions(string? configuredValue, int port)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue)
                && Enum.TryParse<SecureSocketOptions>(configuredValue, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        }

        private bool IsResendConfigured()
        {
            return !string.IsNullOrWhiteSpace(_configuration["Resend:ApiKey"])
                && !string.IsNullOrWhiteSpace(_configuration["Resend:FromEmail"]);
        }
    }
}
