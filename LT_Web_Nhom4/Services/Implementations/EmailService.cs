using LT_Web_Nhom4.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LT_Web_Nhom4.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _configuration["EmailSettings:Host"];
            var port = int.TryParse(_configuration["EmailSettings:Port"], out var configuredPort)
                ? configuredPort
                : 587;
            var senderEmail = _configuration["EmailSettings:Email"];
            var password = _configuration["EmailSettings:Password"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "Quiz Exam System";

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("EmailSettings is incomplete. Skipped sending email '{Subject}' to {Email}.", subject, toEmail);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            // SMTP credentials always come from configuration; secrets are never embedded in code.
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(senderEmail, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
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
