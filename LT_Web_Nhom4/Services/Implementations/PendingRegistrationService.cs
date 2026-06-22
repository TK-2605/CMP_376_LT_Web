using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Services.Implementations
{
    public class PendingRegistrationService : IPendingRegistrationService
    {
        private const int ExpirationMinutes = 15;
        private const int MaxAttempts = 5;
        private readonly ApplicationDbContext _context;

        public PendingRegistrationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PendingRegistrationCreateResult> CreateOrUpdateAsync(
            string email,
            string normalizedEmail,
            string userName,
            string normalizedUserName,
            string fullName,
            string? studentCode,
            string roleName,
            string passwordHash,
            CancellationToken cancellationToken = default)
        {
            await CleanupExpiredAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var secrets = CreateSecrets();
            var salt = CreateSalt();
            var pendingRegistration = await _context.PendingRegistrations
                .FirstOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

            if (pendingRegistration is null)
            {
                pendingRegistration = new PendingRegistration
                {
                    CreatedAtUtc = now,
                    NormalizedEmail = normalizedEmail
                };
                _context.PendingRegistrations.Add(pendingRegistration);
            }

            pendingRegistration.Email = email;
            pendingRegistration.NormalizedEmail = normalizedEmail;
            pendingRegistration.UserName = userName;
            pendingRegistration.NormalizedUserName = normalizedUserName;
            pendingRegistration.FullName = fullName;
            pendingRegistration.StudentCode = studentCode;
            pendingRegistration.RoleName = roleName;
            pendingRegistration.PasswordHash = passwordHash;
            pendingRegistration.TokenSalt = salt;
            pendingRegistration.ConfirmationCodeHash = HashSecret(secrets.Code, salt);
            pendingRegistration.ConfirmationTokenHash = HashSecret(secrets.Token, salt);
            pendingRegistration.ExpiresAtUtc = now.AddMinutes(ExpirationMinutes);
            pendingRegistration.UpdatedAtUtc = now;
            pendingRegistration.AttemptCount = 0;
            pendingRegistration.LastSentAtUtc = now;

            await _context.SaveChangesAsync(cancellationToken);
            return new PendingRegistrationCreateResult(pendingRegistration, secrets.Code, secrets.Token);
        }

        public async Task<PendingRegistrationCreateResult?> ResendAsync(
            string email,
            string normalizedEmail,
            CancellationToken cancellationToken = default)
        {
            await CleanupExpiredAsync(cancellationToken);

            var pendingRegistration = await _context.PendingRegistrations
                .FirstOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

            if (pendingRegistration is null)
            {
                return null;
            }

            var restoreState = new PendingRegistrationRestoreState(
                pendingRegistration.Email,
                pendingRegistration.TokenSalt,
                pendingRegistration.ConfirmationCodeHash,
                pendingRegistration.ConfirmationTokenHash,
                pendingRegistration.ExpiresAtUtc,
                pendingRegistration.UpdatedAtUtc,
                pendingRegistration.AttemptCount,
                pendingRegistration.LastSentAtUtc);
            var now = DateTime.UtcNow;
            var secrets = CreateSecrets();
            var salt = CreateSalt();
            pendingRegistration.Email = email;
            pendingRegistration.TokenSalt = salt;
            pendingRegistration.ConfirmationCodeHash = HashSecret(secrets.Code, salt);
            pendingRegistration.ConfirmationTokenHash = HashSecret(secrets.Token, salt);
            pendingRegistration.ExpiresAtUtc = now.AddMinutes(ExpirationMinutes);
            pendingRegistration.UpdatedAtUtc = now;
            pendingRegistration.AttemptCount = 0;
            pendingRegistration.LastSentAtUtc = now;

            await _context.SaveChangesAsync(cancellationToken);
            return new PendingRegistrationCreateResult(pendingRegistration, secrets.Code, secrets.Token, restoreState);
        }

        public async Task RestoreAsync(
            PendingRegistrationCreateResult pendingResult,
            CancellationToken cancellationToken = default)
        {
            if (pendingResult.RestoreState is null)
            {
                return;
            }

            var pendingRegistration = await _context.PendingRegistrations
                .FirstOrDefaultAsync(item => item.Id == pendingResult.PendingRegistration.Id, cancellationToken);
            if (pendingRegistration is null)
            {
                return;
            }

            var currentCodeHash = HashSecret(pendingResult.Code, pendingRegistration.TokenSalt);
            var currentTokenHash = HashSecret(pendingResult.Token, pendingRegistration.TokenSalt);
            if (!FixedTimeEquals(currentCodeHash, pendingRegistration.ConfirmationCodeHash)
                || !FixedTimeEquals(currentTokenHash, pendingRegistration.ConfirmationTokenHash))
            {
                return;
            }

            pendingRegistration.Email = pendingResult.RestoreState.Email;
            pendingRegistration.TokenSalt = pendingResult.RestoreState.TokenSalt;
            pendingRegistration.ConfirmationCodeHash = pendingResult.RestoreState.ConfirmationCodeHash;
            pendingRegistration.ConfirmationTokenHash = pendingResult.RestoreState.ConfirmationTokenHash;
            pendingRegistration.ExpiresAtUtc = pendingResult.RestoreState.ExpiresAtUtc;
            pendingRegistration.UpdatedAtUtc = pendingResult.RestoreState.UpdatedAtUtc;
            pendingRegistration.AttemptCount = pendingResult.RestoreState.AttemptCount;
            pendingRegistration.LastSentAtUtc = pendingResult.RestoreState.LastSentAtUtc;

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PendingRegistrationValidationResult> ValidateAsync(
            string normalizedEmail,
            string? code,
            string? token,
            CancellationToken cancellationToken = default)
        {
            var pendingRegistration = await _context.PendingRegistrations
                .FirstOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

            if (pendingRegistration is null)
            {
                return new PendingRegistrationValidationResult(PendingRegistrationValidationStatus.NotFound, null);
            }

            if (pendingRegistration.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _context.PendingRegistrations.Remove(pendingRegistration);
                await _context.SaveChangesAsync(cancellationToken);
                return new PendingRegistrationValidationResult(PendingRegistrationValidationStatus.Expired, null);
            }

            if (pendingRegistration.AttemptCount >= MaxAttempts)
            {
                return new PendingRegistrationValidationResult(PendingRegistrationValidationStatus.TooManyAttempts, pendingRegistration);
            }

            var hasValidCode = !string.IsNullOrWhiteSpace(code)
                && FixedTimeEquals(HashSecret(code.Trim(), pendingRegistration.TokenSalt), pendingRegistration.ConfirmationCodeHash);
            var hasValidToken = !string.IsNullOrWhiteSpace(token)
                && FixedTimeEquals(HashSecret(token.Trim(), pendingRegistration.TokenSalt), pendingRegistration.ConfirmationTokenHash);

            if (!hasValidCode && !hasValidToken)
            {
                pendingRegistration.AttemptCount++;
                pendingRegistration.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return new PendingRegistrationValidationResult(PendingRegistrationValidationStatus.InvalidCodeOrToken, pendingRegistration);
            }

            return new PendingRegistrationValidationResult(PendingRegistrationValidationStatus.Valid, pendingRegistration);
        }

        public async Task RemoveAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken = default)
        {
            _context.PendingRegistrations.Remove(pendingRegistration);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var expired = await _context.PendingRegistrations
                .Where(item => item.ExpiresAtUtc <= now)
                .ToListAsync(cancellationToken);

            if (expired.Count == 0)
            {
                return;
            }

            _context.PendingRegistrations.RemoveRange(expired);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static PendingRegistrationSecrets CreateSecrets()
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            return new PendingRegistrationSecrets(code, token);
        }

        private static string CreateSalt()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private static string HashSecret(string value, string salt)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{value}"));
            return Convert.ToHexString(bytes);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}
