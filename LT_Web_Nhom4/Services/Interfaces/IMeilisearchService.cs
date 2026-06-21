namespace LT_Web_Nhom4.Services.Interfaces
{
    public record SearchIndexDocument(
        string Key,
        string Type,
        int EntityId,
        string Title,
        string Meta);

    public record SearchIndexHit(string Type, int EntityId, string Title, string Meta);

    public record MeilisearchHealth(bool Configured, bool Reachable, string Message);

    public interface IMeilisearchService
    {
        Task<IReadOnlyList<SearchIndexHit>?> SearchAsync(
            string query,
            IReadOnlyCollection<SearchIndexDocument> authorizedDocuments,
            CancellationToken cancellationToken = default);

        Task<MeilisearchHealth> GetHealthAsync(CancellationToken cancellationToken = default);
    }
}
