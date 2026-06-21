using Microsoft.AspNetCore.Http;

namespace LT_Web_Nhom4.Services.Interfaces
{
    public interface IPrivateMediaStorage
    {
        Task<string> SaveImageAsync(IFormFile file, string category, CancellationToken cancellationToken = default);

        Task<string> SaveVideoAsync(IFormFile file, string category, CancellationToken cancellationToken = default);

        Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

        Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default);

        string GetContentType(string relativePath);
    }
}
