using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LT_Web_Nhom4.Tests;

public sealed class DeploymentFeatureTests
{
    [Fact]
    public async Task Health_endpoint_is_available()
    {
        await using var factory = new QuizHubWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_google_callback_redirects_instead_of_returning_404()
    {
        await using var factory = new QuizHubWebApplicationFactory();
        using var client = CreateClientWithoutRedirects(factory);

        using var response = await client.GetAsync("/signin-google");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Login/Account/Login?oauthUnavailable=true",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Missing_external_services_are_visible_and_unsafe_actions_are_disabled()
    {
        await using var factory = new QuizHubWebApplicationFactory();
        using var client = factory.CreateClient();

        var loginHtml = await client.GetStringAsync("/Identity/Login/Account/Login");
        var registerHtml = await client.GetStringAsync("/Identity/Login/Account/Register");
        var forgotHtml = await client.GetStringAsync("/Identity/Login/Account/ForgotPassword");

        Assert.Contains("Google OAuth 2.0 chưa cấu hình", loginHtml, StringComparison.Ordinal);
        Assert.Contains("Đăng ký đang tạm dừng", registerHtml, StringComparison.Ordinal);
        Assert.Contains("Chức năng quên mật khẩu đang tạm dừng", forgotHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auth_status_reports_missing_external_services_without_hiding_other_features()
    {
        await using var factory = new QuizHubWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/status");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("googleOAuthConfigured").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("smtpConfigured").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("emailProviderReady").GetBoolean());
        Assert.Equal("Chưa cấu hình", payload.RootElement.GetProperty("emailProvider").GetString());
        Assert.True(payload.RootElement.TryGetProperty("deployCommit", out _));
        Assert.True(payload.RootElement.GetProperty("jwtConfigured").GetBoolean());
    }

    [Fact]
    public async Task Protected_product_routes_redirect_anonymous_users_to_login()
    {
        await using var factory = new QuizHubWebApplicationFactory();
        using var client = CreateClientWithoutRedirects(factory);

        foreach (var route in new[] { "/Classes", "/Exams", "/Chat/Room", "/Admin" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Identity/Login/Account/Login", response.Headers.Location?.PathAndQuery);
        }
    }

    [Fact]
    public async Task Configured_external_services_are_exposed_in_status_and_auth_pages()
    {
        await using var factory = new QuizHubWebApplicationFactory(configureExternalServices: true);
        using var client = factory.CreateClient();

        using var statusResponse = await client.GetAsync("/api/auth/status");
        using var payload = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        var loginHtml = await client.GetStringAsync("/Identity/Login/Account/Login");
        var registerHtml = await client.GetStringAsync("/Identity/Login/Account/Register");

        Assert.True(payload.RootElement.GetProperty("googleOAuthConfigured").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("smtpConfigured").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("emailProviderReady").GetBoolean());
        Assert.Equal("SMTP", payload.RootElement.GetProperty("emailProvider").GetString());
        Assert.Contains("Tiếp tục với Google", loginHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Đăng ký đang tạm dừng", registerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_smtp_only_does_not_report_email_otp_as_ready()
    {
        await using var factory = new QuizHubWebApplicationFactory(
            configureExternalServices: true,
            simulateRenderRuntime: true);
        using var client = factory.CreateClient();

        using var statusResponse = await client.GetAsync("/api/auth/status");
        using var payload = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        var registerHtml = await client.GetStringAsync("/Identity/Login/Account/Register");
        var forgotHtml = await client.GetStringAsync("/Identity/Login/Account/ForgotPassword");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.False(payload.RootElement.GetProperty("smtpConfigured").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("emailProviderReady").GetBoolean());
        Assert.Equal("SMTP (blocked on Render Free)", payload.RootElement.GetProperty("emailProvider").GetString());
        Assert.Contains("Gmail API", payload.RootElement.GetProperty("emailProviderProblem").GetString(), StringComparison.Ordinal);
        Assert.Contains("Đăng ký đang tạm dừng", registerHtml, StringComparison.Ordinal);
        Assert.Contains("Chức năng quên mật khẩu đang tạm dừng", forgotHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_https_email_provider_reports_email_otp_as_ready()
    {
        await using var factory = new QuizHubWebApplicationFactory(
            configureExternalServices: true,
            simulateRenderRuntime: true,
            configureHttpsEmailProvider: true);
        using var client = factory.CreateClient();

        using var statusResponse = await client.GetAsync("/api/auth/status");
        using var payload = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        var registerHtml = await client.GetStringAsync("/Identity/Login/Account/Register");
        var forgotHtml = await client.GetStringAsync("/Identity/Login/Account/ForgotPassword");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.True(payload.RootElement.GetProperty("smtpConfigured").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("emailProviderReady").GetBoolean());
        Assert.Equal("Brevo HTTPS", payload.RootElement.GetProperty("emailProvider").GetString());
        Assert.DoesNotContain("Đăng ký đang tạm dừng", registerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Chức năng quên mật khẩu đang tạm dừng", forgotHtml, StringComparison.Ordinal);
    }

    private static HttpClient CreateClientWithoutRedirects(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}

internal sealed class QuizHubWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _configureExternalServices;
    private readonly bool _simulateRenderRuntime;
    private readonly bool _configureHttpsEmailProvider;

    public QuizHubWebApplicationFactory(
        bool configureExternalServices = false,
        bool simulateRenderRuntime = false,
        bool configureHttpsEmailProvider = false)
    {
        _configureExternalServices = configureExternalServices;
        _simulateRenderRuntime = simulateRenderRuntime;
        _configureHttpsEmailProvider = configureHttpsEmailProvider;
        Environment.SetEnvironmentVariable(
            "Authentication__Google__ClientId",
            configureExternalServices ? "test-client-id" : string.Empty);
        Environment.SetEnvironmentVariable(
            "Authentication__Google__ClientSecret",
            configureExternalServices ? "test-client-secret" : string.Empty);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=QuizHubTests;User Id=sa;Password=NotUsed_Test1!;TrustServerCertificate=True",
                ["Jwt:Issuer"] = "QuizHub.Tests",
                ["Jwt:Audience"] = "QuizHub.Tests",
                ["Jwt:Key"] = "QuizHubTests_Jwt_Key_With_At_Least_Thirty_Two_Characters",
                ["Swagger:Enabled"] = "true",
                ["ForwardedHeaders:Enabled"] = "false"
            };

            if (_configureExternalServices)
            {
                values["Authentication:Google:ClientId"] = "test-client-id";
                values["Authentication:Google:ClientSecret"] = "test-client-secret";
                values["Smtp:Host"] = _simulateRenderRuntime ? "smtp.gmail.com" : "smtp.example.test";
                values["Smtp:UserName"] = "test-user";
                values["Smtp:Password"] = "test-password";
                values["Smtp:FromEmail"] = "test@example.test";
            }
            else
            {
                values["Authentication:Google:ClientId"] = string.Empty;
                values["Authentication:Google:ClientSecret"] = string.Empty;
                values["Smtp:UserName"] = string.Empty;
                values["Smtp:Password"] = string.Empty;
                values["Smtp:FromEmail"] = string.Empty;
            }

            if (_simulateRenderRuntime)
            {
                values["RENDER"] = "true";
                values["RENDER_SERVICE_ID"] = "srv-test";
            }

            if (_configureHttpsEmailProvider)
            {
                values["Email:Provider"] = "Brevo";
                values["Brevo:ApiKey"] = "test-brevo-key";
                values["Brevo:FromEmail"] = "noreply@example.test";
            }

            configuration.AddInMemoryCollection(values);
        });
    }
}
