using LT_Web_Nhom4.Services.Interfaces;
using LT_Web_Nhom4.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;

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
            var provider = _configuration["Email:Provider"];
            var smtpConfigured = IsSmtpConfigured();
            var resendConfigured = IsResendConfigured();

            if (string.Equals(provider, "Resend", StringComparison.OrdinalIgnoreCase) && resendConfigured)
            {
                try
                {
                    await SendWithResendAsync(email, subject, htmlMessage);
                    return;
                }
                catch (Exception exception) when (smtpConfigured)
                {
                    _logger.LogWarning(exception, "Resend failed for '{Subject}'. Falling back to SMTP.", subject);
                    await SendWithSmtpAsync(email, subject, htmlMessage);
                    return;
                }
            }

            if (smtpConfigured)
            {
                await SendWithSmtpAsync(email, subject, htmlMessage);
                return;
            }

            if (resendConfigured)
            {
                await SendWithResendAsync(email, subject, htmlMessage);
                return;
            }

            throw new InvalidOperationException(
                EmailConfigurationHelper.GetEmailProviderProblem(_configuration)
                ?? "No email provider is configured.");
        }

        private async Task SendWithSmtpAsync(string email, string subject, string htmlMessage)
        {
            if (EmailConfigurationHelper.IsSmtpBlockedByRuntime(_configuration))
            {
                throw new InvalidOperationException(
                    EmailConfigurationHelper.GetEmailProviderProblem(_configuration)
                    ?? "SMTP is not usable in the current runtime.");
            }

            var host = EmailConfigurationHelper.GetSmtpHost(_configuration);
            var port = EmailConfigurationHelper.GetSmtpPort(_configuration);
            var userName = EmailConfigurationHelper.GetSmtpUserName(_configuration);
            var password = EmailConfigurationHelper.GetSmtpPassword(_configuration);
            var fromEmail = EmailConfigurationHelper.GetSmtpFromEmail(_configuration);
            var fromName = EmailConfigurationHelper.GetSmtpFromName(_configuration) ?? "QuizHub";
            var timeoutSeconds = EmailConfigurationHelper.GetSmtpTimeoutSeconds(_configuration);
            var secureSocketOptions = ResolveSecureSocketOptions(
                EmailConfigurationHelper.GetSmtpSecureSocketOptions(_configuration),
                port);

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("SMTP configuration is incomplete. Email '{Subject}' was not sent to {Email}.", subject, email);
                throw new InvalidOperationException("SMTP chưa được cấu hình đầy đủ.");
            }

            var attempts = BuildSmtpAttempts(host, port, secureSocketOptions);
            Exception? lastException = null;
            for (var index = 0; index < attempts.Count; index++)
            {
                var attempt = attempts[index];
                try
                {
                    await SendWithSmtpAttemptAsync(
                        attempt.Host,
                        attempt.Port,
                        attempt.Options,
                        timeoutSeconds,
                        userName,
                        password,
                        fromEmail,
                        fromName,
                        email,
                        subject,
                        htmlMessage);
                    return;
                }
                catch (Exception exception) when (index < attempts.Count - 1 && IsTransientSmtpConnectionFailure(exception))
                {
                    lastException = exception;
                    _logger.LogWarning(
                        exception,
                        "SMTP attempt {Host}:{Port}/{Options} timed out or could not connect. Trying fallback port.",
                        attempt.Host,
                        attempt.Port,
                        attempt.Options);
                }
            }

            if (lastException is not null)
            {
                throw lastException;
            }

            throw new InvalidOperationException("SMTP email was not sent.");
        }

        private async Task SendWithSmtpAttemptAsync(
            string host,
            int port,
            SecureSocketOptions secureSocketOptions,
            int timeoutSeconds,
            string userName,
            string password,
            string fromEmail,
            string fromName,
            string email,
            string subject,
            string htmlMessage)
        {
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

            _logger.LogInformation("SMTP email '{Subject}' sent to {Email} via {Host}:{Port}/{Options}.", subject, email, host, port, secureSocketOptions);
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

        private static IReadOnlyList<(string Host, int Port, SecureSocketOptions Options)> BuildSmtpAttempts(
            string host,
            int port,
            SecureSocketOptions options)
        {
            var attempts = new List<(string Host, int Port, SecureSocketOptions Options)>
            {
                (host, port, options)
            };

            if (string.Equals(host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                AddIfMissing(587, SecureSocketOptions.StartTls);
                AddIfMissing(465, SecureSocketOptions.SslOnConnect);
            }

            return attempts;

            void AddIfMissing(int fallbackPort, SecureSocketOptions fallbackOptions)
            {
                if (!attempts.Any(item => item.Port == fallbackPort && item.Options == fallbackOptions))
                {
                    attempts.Add((host, fallbackPort, fallbackOptions));
                }
            }
        }

        private static bool IsTransientSmtpConnectionFailure(Exception exception)
        {
            return exception is TaskCanceledException
                || exception is TimeoutException
                || exception is SocketException
                || exception is IOException
                || (exception.InnerException is not null && IsTransientSmtpConnectionFailure(exception.InnerException));
        }

        private bool IsSmtpConfigured()
        {
            return EmailConfigurationHelper.HasUsableSmtpProvider(_configuration);
        }

        private bool IsResendConfigured()
        {
            return EmailConfigurationHelper.HasResendProvider(_configuration);
        }
    }
}
