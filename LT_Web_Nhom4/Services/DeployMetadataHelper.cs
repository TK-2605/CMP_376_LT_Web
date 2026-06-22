using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace LT_Web_Nhom4.Services
{
    public sealed record DeployMetadata(
        string? CommitSha,
        string? CommitShortSha,
        string Environment,
        bool IsRender,
        string? RenderServiceId);

    public static class DeployMetadataHelper
    {
        private static readonly string[] CommitKeys =
        [
            "RENDER_GIT_COMMIT",
            "RENDER_COMMIT",
            "SOURCE_COMMIT",
            "GITHUB_SHA",
            "COMMIT_SHA"
        ];

        public static DeployMetadata Get(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var commitSha = GetCommitSha(configuration);
            return new DeployMetadata(
                commitSha,
                ShortSha(commitSha),
                environment.EnvironmentName,
                IsRenderRuntime(configuration),
                FirstConfigured(configuration, "RENDER_SERVICE_ID", "RENDER_EXTERNAL_URL"));
        }

        public static string? GetCommitSha(IConfiguration configuration)
        {
            var configured = FirstConfigured(configuration, CommitKeys);
            return NormalizeCommit(configured)
                ?? NormalizeCommit(Assembly.GetEntryAssembly()
                    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion);
        }

        private static string? NormalizeCommit(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            var plusIndex = normalized.LastIndexOf('+');
            if (plusIndex >= 0 && plusIndex < normalized.Length - 1)
            {
                normalized = normalized[(plusIndex + 1)..];
            }

            return normalized.Length == 0 ? null : normalized;
        }

        private static string? ShortSha(string? commitSha)
        {
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return null;
            }

            return commitSha.Length <= 12 ? commitSha : commitSha[..12];
        }

        private static bool IsRenderRuntime(IConfiguration configuration)
        {
            return !string.IsNullOrWhiteSpace(configuration["RENDER"])
                || !string.IsNullOrWhiteSpace(configuration["RENDER_SERVICE_ID"])
                || !string.IsNullOrWhiteSpace(configuration["RENDER_EXTERNAL_URL"])
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_SERVICE_ID"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
        }

        private static string? FirstConfigured(IConfiguration configuration, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
