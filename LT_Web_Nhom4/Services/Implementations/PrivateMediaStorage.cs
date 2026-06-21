using LT_Web_Nhom4.Services.Interfaces;

namespace LT_Web_Nhom4.Services.Implementations
{
    public sealed class PrivateMediaStorage : IPrivateMediaStorage
    {
        private const long MaxImageBytes = 5 * 1024 * 1024;
        private const long MaxVideoBytes = 100 * 1024 * 1024;

        private static readonly IReadOnlyDictionary<string, string> AllowedImageExtensions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".webp"] = "image/webp"
            };

        private static readonly IReadOnlyDictionary<string, string> AllowedVideoExtensions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".mp4"] = "video/mp4",
                [".webm"] = "video/webm",
                [".mov"] = "video/quicktime"
            };

        private readonly string _rootPath;

        public PrivateMediaStorage(IWebHostEnvironment environment, IConfiguration configuration)
        {
            var configuredPath = configuration["PrivateMediaRoot"];
            _rootPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(environment.ContentRootPath, "App_Data", "Uploads")
                : Path.GetFullPath(configuredPath);
        }

        public async Task<string> SaveImageAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
        {
            if (file.Length <= 0 || file.Length > MaxImageBytes)
            {
                throw new InvalidOperationException("Ảnh phải có dung lượng từ 1 byte đến 5 MB.");
            }

            return await SaveFileAsync(file, category, AllowedImageExtensions, "Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.", cancellationToken);
        }

        public async Task<string> SaveVideoAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
        {
            if (file.Length <= 0 || file.Length > MaxVideoBytes)
            {
                throw new InvalidOperationException("Video phải có dung lượng từ 1 byte đến 100 MB.");
            }

            return await SaveFileAsync(file, category, AllowedVideoExtensions, "Chỉ chấp nhận video MP4, WebM hoặc MOV.", cancellationToken);
        }

        public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var absolutePath = ResolvePath(relativePath);
            Stream? stream = File.Exists(absolutePath)
                ? new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
                : null;
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                var absolutePath = ResolvePath(relativePath);
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }

            return Task.CompletedTask;
        }

        public string GetContentType(string relativePath)
        {
            var extension = Path.GetExtension(relativePath);
            if (AllowedImageExtensions.TryGetValue(extension, out var imageContentType))
            {
                return imageContentType;
            }

            return AllowedVideoExtensions.TryGetValue(extension, out var videoContentType)
                ? videoContentType
                : "application/octet-stream";
        }

        private async Task<string> SaveFileAsync(
            IFormFile file,
            string category,
            IReadOnlyDictionary<string, string> allowedExtensions,
            string invalidTypeMessage,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.TryGetValue(extension, out var expectedContentType)
                || !string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(invalidTypeMessage);
            }

            var safeCategory = NormalizeCategory(category);
            var directory = Path.Combine(_rootPath, safeCategory);
            Directory.CreateDirectory(directory);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(directory, fileName);

            await using var stream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await file.CopyToAsync(stream, cancellationToken);
            return $"{safeCategory}/{fileName}";
        }

        private static string NormalizeCategory(string category)
        {
            var safeCategory = new string(category.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return string.IsNullOrWhiteSpace(safeCategory) ? "media" : safeCategory;
        }

        private string ResolvePath(string relativePath)
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
            var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Đường dẫn media không hợp lệ.");
            }

            return absolutePath;
        }
    }
}
