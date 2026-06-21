using System.Net.Http.Json;
using System.Text.Json;
using LT_Web_Nhom4.Services.Interfaces;

namespace LT_Web_Nhom4.Services.Implementations
{
    public sealed class MeilisearchService : IMeilisearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MeilisearchService> _logger;

        public MeilisearchService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<MeilisearchService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            var apiKey = configuration["Meilisearch:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        public async Task<IReadOnlyList<SearchIndexHit>?> SearchAsync(
            string query,
            IReadOnlyCollection<SearchIndexDocument> authorizedDocuments,
            CancellationToken cancellationToken = default)
        {
            var baseUrl = GetBaseUrl();
            if (baseUrl is null || authorizedDocuments.Count == 0)
            {
                return null;
            }

            try
            {
                var indexName = Uri.EscapeDataString(GetIndexName());
                var documentsUrl = $"{baseUrl}/indexes/{indexName}/documents?primaryKey=key";
                using var indexResponse = await _httpClient.PostAsJsonAsync(
                    documentsUrl,
                    authorizedDocuments.Select(item => new
                    {
                        key = item.Key,
                        type = item.Type,
                        entityId = item.EntityId,
                        title = item.Title,
                        meta = item.Meta
                    }),
                    cancellationToken);
                indexResponse.EnsureSuccessStatusCode();

                using var searchResponse = await _httpClient.PostAsJsonAsync(
                    $"{baseUrl}/indexes/{indexName}/search",
                    new { q = query, limit = 50 },
                    cancellationToken);
                searchResponse.EnsureSuccessStatusCode();

                await using var stream = await searchResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!json.RootElement.TryGetProperty("hits", out var hits))
                {
                    return null;
                }

                var authorizedKeys = authorizedDocuments.ToDictionary(item => item.Key, StringComparer.Ordinal);
                var results = new List<SearchIndexHit>();
                foreach (var hit in hits.EnumerateArray())
                {
                    if (!hit.TryGetProperty("key", out var keyElement)
                        || !authorizedKeys.TryGetValue(keyElement.GetString() ?? string.Empty, out var document))
                    {
                        continue;
                    }

                    results.Add(new SearchIndexHit(document.Type, document.EntityId, document.Title, document.Meta));
                    if (results.Count == 8)
                    {
                        break;
                    }
                }

                // Meilisearch indexes updates asynchronously. The SQL fallback supplies the first request.
                return results.Count == 0 ? null : results;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _logger.LogDebug(exception, "Meilisearch is unavailable; scoped SQL search will be used.");
                return null;
            }
        }

        public async Task<MeilisearchHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            var baseUrl = GetBaseUrl();
            if (baseUrl is null)
            {
                return new MeilisearchHealth(false, false, "Chưa cấu hình địa chỉ Meilisearch.");
            }

            try
            {
                using var response = await _httpClient.GetAsync($"{baseUrl}/health", cancellationToken);
                return response.IsSuccessStatusCode
                    ? new MeilisearchHealth(true, true, "Dịch vụ tìm kiếm đang phản hồi.")
                    : new MeilisearchHealth(true, false, $"Dịch vụ trả về HTTP {(int)response.StatusCode}.");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return new MeilisearchHealth(true, false, "Không kết nối được Meilisearch; hệ thống đang dùng tìm kiếm SQL dự phòng.");
            }
        }

        private string? GetBaseUrl()
        {
            var value = _configuration["Meilisearch:Url"]?.Trim().TrimEnd('/');
            return Uri.TryCreate(value, UriKind.Absolute, out _) ? value : null;
        }

        private string GetIndexName()
        {
            return _configuration["Meilisearch:IndexName"]?.Trim() is { Length: > 0 } value
                ? value
                : "quizhub-private-search";
        }
    }
}
