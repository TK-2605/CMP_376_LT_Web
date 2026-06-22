using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Services.Implementations
{
    public sealed class PrivateMediaStorage : IPrivateMediaStorage
    {
        private const string DatabasePathPrefix = "db:";
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
        private readonly ApplicationDbContext _context;
        private readonly bool _useDatabaseStorage;

        public PrivateMediaStorage(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _context = context;
            var configuredPath = configuration["PrivateMediaRoot"];
            _rootPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(environment.ContentRootPath, "App_Data", "Uploads")
                : Path.GetFullPath(configuredPath);
            _useDatabaseStorage = string.Equals(
                configuration["Media:StorageProvider"],
                "Database",
                StringComparison.OrdinalIgnoreCase);
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
            if (TryParseDatabasePath(relativePath, out var id))
            {
                return OpenDatabaseReadAsync(id, cancellationToken);
            }

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
                if (TryParseDatabasePath(relativePath, out var id))
                {
                    return DeleteDatabaseFileAsync(id, cancellationToken);
                }

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
            if (_useDatabaseStorage)
            {
                var id = Guid.NewGuid();
                await using var memory = new MemoryStream();
                await file.CopyToAsync(memory, cancellationToken);
                await InsertDatabaseFileAsync(
                    id,
                    safeCategory,
                    Path.GetFileName(file.FileName),
                    expectedContentType,
                    file.Length,
                    memory.ToArray(),
                    cancellationToken);
                return $"{DatabasePathPrefix}{id:N}{extension}";
            }

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

        private async Task<Stream?> OpenDatabaseReadAsync(Guid id, CancellationToken cancellationToken)
        {
            var content = await _context.StoredMediaFiles.AsNoTracking()
                .Where(media => media.Id == id)
                .Select(media => media.Content)
                .FirstOrDefaultAsync(cancellationToken);
            return content is null ? null : new MemoryStream(content, writable: false);
        }

        private async Task InsertDatabaseFileAsync(
            Guid id,
            string category,
            string originalFileName,
            string contentType,
            long length,
            byte[] content,
            CancellationToken cancellationToken)
        {
            var createdAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var provider = _context.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "StoredMediaFiles" ("Id", "Category", "OriginalFileName", "ContentType", "Length", "Content", "CreatedAt")
                    VALUES ({id}, {category}, {originalFileName}, {contentType}, {length}, {content}, {createdAt})
                    """, cancellationToken);
                return;
            }

            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [StoredMediaFiles] ([Id], [Category], [OriginalFileName], [ContentType], [Length], [Content], [CreatedAt])
                VALUES ({id}, {category}, {originalFileName}, {contentType}, {length}, {content}, {createdAt})
                """, cancellationToken);
        }

        private async Task DeleteDatabaseFileAsync(Guid id, CancellationToken cancellationToken)
        {
            var provider = _context.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "StoredMediaFiles" WHERE "Id" = {id}
                    """, cancellationToken);
                return;
            }

            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM [StoredMediaFiles] WHERE [Id] = {id}
                """, cancellationToken);
        }

        private static bool TryParseDatabasePath(string relativePath, out Guid id)
        {
            id = Guid.Empty;
            if (!relativePath.StartsWith(DatabasePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var value = relativePath[DatabasePathPrefix.Length..];
            var extensionIndex = value.IndexOf('.', StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                value = value[..extensionIndex];
            }

            return Guid.TryParseExact(value, "N", out id) || Guid.TryParse(value, out id);
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
