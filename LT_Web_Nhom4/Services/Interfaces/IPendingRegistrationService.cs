using LT_Web_Nhom4.Models;

namespace LT_Web_Nhom4.Services.Interfaces
{
    public record PendingRegistrationSecrets(string Code, string Token);

    public record PendingRegistrationCreateResult(PendingRegistration PendingRegistration, string Code, string Token);

    public enum PendingRegistrationValidationStatus
    {
        Valid,
        NotFound,
        Expired,
        TooManyAttempts,
        InvalidCodeOrToken
    }

    public record PendingRegistrationValidationResult(
        PendingRegistrationValidationStatus Status,
        PendingRegistration? PendingRegistration);

    public interface IPendingRegistrationService
    {
        Task<PendingRegistrationCreateResult> CreateOrUpdateAsync(
            string email,
            string normalizedEmail,
            string userName,
            string normalizedUserName,
            string fullName,
            string? studentCode,
            string roleName,
            string passwordHash,
            CancellationToken cancellationToken = default);

        Task<PendingRegistrationCreateResult?> ResendAsync(
            string email,
            string normalizedEmail,
            CancellationToken cancellationToken = default);

        Task<PendingRegistrationValidationResult> ValidateAsync(
            string normalizedEmail,
            string? code,
            string? token,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken = default);

        Task CleanupExpiredAsync(CancellationToken cancellationToken = default);
    }
}
