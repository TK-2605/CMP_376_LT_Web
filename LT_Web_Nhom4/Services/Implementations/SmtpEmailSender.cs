using LT_Web_Nhom4.Services.Interfaces;
using LT_Web_Nhom4.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net;

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
            Exception? lastProviderException = null;

            foreach (var providerName in BuildProviderOrder(provider))
            {
                try
                {
                    if ((string.Equals(providerName, "GmailApi", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(providerName, "Gmail", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(providerName, "Google", StringComparison.OrdinalIgnoreCase))
                        && IsGmailApiConfigured())
                    {
                        await SendWithGmailApiAsync(email, subject, htmlMessage);
                        return;
                    }

                    if (string.Equals(providerName, "Resend", StringComparison.OrdinalIgnoreCase)
                        && IsResendConfigured())
                    {
                        await SendWithResendAsync(email, subject, htmlMessage);
                        return;
                    }

                    if (string.Equals(providerName, "Brevo", StringComparison.OrdinalIgnoreCase)
                        && IsBrevoConfigured())
                    {
                        await SendWithBrevoAsync(email, subject, htmlMessage);
                        return;
                    }

                    if (string.Equals(providerName, "SendGrid", StringComparison.OrdinalIgnoreCase)
                        && IsSendGridConfigured())
                    {
                        await SendWithSendGridAsync(email, subject, htmlMessage);
                        return;
                    }

                    if (string.Equals(providerName, "Smtp", StringComparison.OrdinalIgnoreCase)
                        && smtpConfigured)
                    {
                        await SendWithSmtpAsync(email, subject, htmlMessage);
                        return;
                    }
                }
                catch (Exception exception)
                {
                    lastProviderException = exception;
                    _logger.LogWarning(
                        exception,
                        "Email provider {Provider} failed for {Email}. Trying next configured provider if available.",
                        providerName,
                        MaskEmail(email));
                }
            }

            if (lastProviderException is not null)
            {
                throw new InvalidOperationException(
                    $"All configured email providers failed. Last provider error: {lastProviderException.Message}",
                    lastProviderException);
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
                _logger.LogWarning("SMTP configuration is incomplete. Email '{Subject}' was not sent to {Email}.", subject, MaskEmail(email));
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
            message.Body = BuildMimeBody(htmlMessage);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var client = new SmtpClient
            {
                Timeout = timeoutSeconds * 1000
            };

            await client.ConnectAsync(host, port, secureSocketOptions, timeout.Token);
            await client.AuthenticateAsync(userName, password, timeout.Token);
            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);

            _logger.LogInformation("SMTP email '{Subject}' sent to {Email} via {Host}:{Port}/{Options}.", subject, MaskEmail(email), host, port, secureSocketOptions);
        }

        private async Task SendWithResendAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _configuration["Resend:ApiKey"];
            var fromEmail = _configuration["Resend:FromEmail"];
            var fromName = _configuration["Resend:FromName"] ?? _configuration["Smtp:FromName"] ?? "QuizHub";
            var timeoutSeconds = GetHttpProviderTimeoutSeconds("Resend");

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
                    html = htmlMessage,
                    text = ConvertHtmlToPlainText(htmlMessage)
                },
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = SanitizeProviderBody(await response.Content.ReadAsStringAsync(timeout.Token));
                _logger.LogError(
                    "Resend email provider failed. CorrelationId {CorrelationId}, To {ToEmail}, HTTP {StatusCode}, Body {Body}",
                    CreateCorrelationId(),
                    MaskEmail(email),
                    (int)response.StatusCode,
                    body);
                throw new InvalidOperationException($"Resend send failed HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("Resend email '{Subject}' sent to {Email}.", subject, MaskEmail(email));
        }

        private async Task SendWithGmailApiAsync(string email, string subject, string htmlMessage)
        {
            var correlationId = CreateCorrelationId();
            var clientId = EmailConfigurationHelper.GetGmailApiClientId(_configuration);
            var clientSecret = EmailConfigurationHelper.GetGmailApiClientSecret(_configuration);
            var refreshToken = _configuration["GmailApi:RefreshToken"];
            var fromEmail = _configuration["GmailApi:FromEmail"];
            var fromName = _configuration["GmailApi:FromName"] ?? _configuration["Smtp:FromName"] ?? "QuizHub";
            var timeoutSeconds = GetHttpProviderTimeoutSeconds("GmailApi");

            if (string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(clientSecret)
                || string.IsNullOrWhiteSpace(refreshToken)
                || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("Gmail API email provider is not configured.");
            }

            _logger.LogInformation(
                "Gmail API send starting. CorrelationId {CorrelationId}, From {FromEmail}, To {ToEmail}.",
                correlationId,
                MaskEmail(fromEmail),
                MaskEmail(email));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            var accessToken = await GetGmailAccessTokenAsync(
                httpClient,
                clientId,
                clientSecret,
                refreshToken,
                correlationId,
                timeout.Token);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = BuildMimeBody(htmlMessage);

            await using var stream = new MemoryStream();
            await message.WriteToAsync(stream, timeout.Token);
            var raw = Convert.ToBase64String(stream.ToArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new { raw });

            using var response = await httpClient.SendAsync(request, timeout.Token);
            var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = SanitizeProviderBody(responseBody);
                _logger.LogError(
                    "Gmail API send failed. CorrelationId {CorrelationId}, From {FromEmail}, To {ToEmail}, HTTP {StatusCode}, Body {Body}",
                    correlationId,
                    MaskEmail(fromEmail),
                    MaskEmail(email),
                    (int)response.StatusCode,
                    body);
                throw new InvalidOperationException($"Gmail API send failed HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation(
                "Gmail API email '{Subject}' sent. CorrelationId {CorrelationId}, From {FromEmail}, To {ToEmail}, HTTP {StatusCode}.",
                subject,
                correlationId,
                MaskEmail(fromEmail),
                MaskEmail(email),
                (int)response.StatusCode);
            _logger.LogInformation(
                "Gmail API response. CorrelationId {CorrelationId}, Body {Body}.",
                correlationId,
                SanitizeProviderBody(responseBody));
        }

        private async Task<string> GetGmailAccessTokenAsync(
            HttpClient httpClient,
            string clientId,
            string clientSecret,
            string refreshToken,
            string correlationId,
            CancellationToken cancellationToken)
        {
            using var tokenResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                }),
                cancellationToken);

            var body = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var sanitizedBody = SanitizeProviderBody(body);
                _logger.LogError(
                    "Gmail API token endpoint failed. CorrelationId {CorrelationId}, HTTP {StatusCode}, Body {Body}",
                    correlationId,
                    (int)tokenResponse.StatusCode,
                    sanitizedBody);
                throw new InvalidOperationException($"Gmail API token endpoint failed HTTP {(int)tokenResponse.StatusCode}: {sanitizedBody}");
            }

            _logger.LogInformation(
                "Gmail API token endpoint succeeded. CorrelationId {CorrelationId}, HTTP {StatusCode}.",
                correlationId,
                (int)tokenResponse.StatusCode);

            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                throw new InvalidOperationException("Gmail token endpoint did not return access_token.");
            }

            var accessToken = accessTokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Gmail token endpoint returned an empty access_token.");
            }

            return accessToken;
        }

        private async Task SendWithBrevoAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _configuration["Brevo:ApiKey"];
            var fromEmail = _configuration["Brevo:FromEmail"];
            var fromName = _configuration["Brevo:FromName"] ?? _configuration["Smtp:FromName"] ?? "QuizHub";
            var timeoutSeconds = GetHttpProviderTimeoutSeconds("Brevo");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("Brevo email provider is not configured.");
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

            using var response = await httpClient.PostAsJsonAsync(
                "https://api.brevo.com/v3/smtp/email",
                new
                {
                    sender = new { name = fromName, email = fromEmail },
                    to = new[] { new { email } },
                    subject,
                    htmlContent = htmlMessage,
                    textContent = ConvertHtmlToPlainText(htmlMessage)
                },
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = SanitizeProviderBody(await response.Content.ReadAsStringAsync(timeout.Token));
                _logger.LogError(
                    "Brevo email provider failed. CorrelationId {CorrelationId}, To {ToEmail}, HTTP {StatusCode}, Body {Body}",
                    CreateCorrelationId(),
                    MaskEmail(email),
                    (int)response.StatusCode,
                    body);
                throw new InvalidOperationException($"Brevo send failed HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("Brevo email '{Subject}' sent to {Email}.", subject, MaskEmail(email));
        }

        private async Task SendWithSendGridAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _configuration["SendGrid:ApiKey"];
            var fromEmail = _configuration["SendGrid:FromEmail"];
            var fromName = _configuration["SendGrid:FromName"] ?? _configuration["Smtp:FromName"] ?? "QuizHub";
            var timeoutSeconds = GetHttpProviderTimeoutSeconds("SendGrid");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("SendGrid email provider is not configured.");
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await httpClient.PostAsJsonAsync(
                "https://api.sendgrid.com/v3/mail/send",
                new
                {
                    personalizations = new[]
                    {
                        new { to = new[] { new { email } } }
                    },
                    from = new { email = fromEmail, name = fromName },
                    subject,
                    content = new[]
                    {
                        new { type = "text/plain", value = ConvertHtmlToPlainText(htmlMessage) },
                        new { type = "text/html", value = htmlMessage }
                    }
                },
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = SanitizeProviderBody(await response.Content.ReadAsStringAsync(timeout.Token));
                _logger.LogError(
                    "SendGrid email provider failed. CorrelationId {CorrelationId}, To {ToEmail}, HTTP {StatusCode}, Body {Body}",
                    CreateCorrelationId(),
                    MaskEmail(email),
                    (int)response.StatusCode,
                    body);
                throw new InvalidOperationException($"SendGrid send failed HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("SendGrid email '{Subject}' sent to {Email}.", subject, MaskEmail(email));
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

        private bool IsGmailApiConfigured()
        {
            return EmailConfigurationHelper.HasGmailApiProvider(_configuration);
        }

        private bool IsBrevoConfigured()
        {
            return EmailConfigurationHelper.HasBrevoProvider(_configuration);
        }

        private bool IsSendGridConfigured()
        {
            return EmailConfigurationHelper.HasSendGridProvider(_configuration);
        }

        private int GetHttpProviderTimeoutSeconds(string providerName)
        {
            return int.TryParse(_configuration[$"{providerName}:TimeoutSeconds"], out var configuredTimeout)
                ? Math.Clamp(configuredTimeout, 5, 60)
                : 20;
        }

        private static IReadOnlyList<string> BuildProviderOrder(string? configuredProvider)
        {
            var providers = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredProvider))
            {
                providers.Add(configuredProvider.Trim());
            }

            providers.AddRange(["GmailApi", "Resend", "Brevo", "SendGrid", "Smtp"]);
            return providers
                .Where(provider => !string.IsNullOrWhiteSpace(provider))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string CreateCorrelationId()
        {
            return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "<empty>";
            }

            var trimmed = email.Trim();
            var atIndex = trimmed.IndexOf('@');
            if (atIndex <= 1)
            {
                return "***";
            }

            var local = trimmed[..atIndex];
            var domain = trimmed[(atIndex + 1)..];
            var visibleLocal = local.Length <= 2 ? local[..1] : local[..Math.Min(2, local.Length)];
            return $"{visibleLocal}***@{domain}";
        }

        private static string SanitizeProviderBody(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "<empty>";
            }

            var normalized = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (normalized.Contains("access_token", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("refresh_token", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("client_secret", StringComparison.OrdinalIgnoreCase))
            {
                return "[redacted provider response containing token material]";
            }

            const int maxLength = 1000;
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }

        private static MimeEntity BuildMimeBody(string htmlMessage)
        {
            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage,
                TextBody = ConvertHtmlToPlainText(htmlMessage)
            };

            return builder.ToMessageBody();
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var withBreaks = Regex.Replace(html, "</(p|div|br|li|h[1-6])>", "\n", RegexOptions.IgnoreCase);
            var withoutTags = Regex.Replace(withBreaks, "<[^>]+>", " ");
            var decoded = WebUtility.HtmlDecode(withoutTags);
            return Regex.Replace(decoded, @"[ \t]+", " ")
                .Replace("\n ", "\n")
                .Trim();
        }
    }
}
