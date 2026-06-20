using LT_Web_Nhom4.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

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
            var host = _configuration["Smtp:Host"];
            var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
            var userName = _configuration["Smtp:UserName"];
            var password = _configuration["Smtp:Password"];
            var fromEmail = _configuration["Smtp:FromEmail"];
            var fromName = _configuration["Smtp:FromName"] ?? "LT_Web_Nhom4";

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogError("SMTP configuration is incomplete. Cannot send email '{Subject}' to {Email}.", subject, email);
                throw new InvalidOperationException("SMTP configuration is incomplete.");
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(email));
                message.Subject = subject;
                message.Body = new TextPart("html") { Text = htmlMessage };

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(userName, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("SMTP email '{Subject}' sent to {Email}.", subject, email);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to send SMTP email '{Subject}' to {Email}.", subject, email);
                throw;
            }
        }

        public Task SendOtpEmailAsync(string toEmail, string otp, string purpose)
        {
            var isConfirmEmail = string.Equals(purpose, "ConfirmEmail", StringComparison.OrdinalIgnoreCase);
            var subject = isConfirmEmail ? "Ma xac nhan email" : "Ma dat lai mat khau";
            var htmlBody = $"""
                <p>Ma OTP cua ban la: <strong>{otp}</strong></p>
                <p>Ma co hieu luc trong 5 phut. Vui long khong chia se ma nay.</p>
                """;

            return SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}
