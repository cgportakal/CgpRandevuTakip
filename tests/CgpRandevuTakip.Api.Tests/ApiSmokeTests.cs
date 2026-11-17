using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CgpRandevuTakip.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CgpRandevuTakip.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "cgp-api-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Data:Root", _root);
        builder.UseSetting("Jwt:Key", new string('k', 48));
        builder.UseSetting("EnableSwagger", "false");
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }
}

public class ApiSmokeTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public ApiSmokeTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsVersion()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("ok", payload.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task VersionPolicy_IsPublic()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/version-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var policy = await response.Content.ReadFromJsonAsync<VersionPolicyDto>(JsonOptions);
        Assert.NotNull(policy);
        Assert.False(string.IsNullOrWhiteSpace(policy!.MinDesktopVersion));
        Assert.False(string.IsNullOrWhiteSpace(policy.MinMobileVersion));
        Assert.False(string.IsNullOrWhiteSpace(policy.MinWebVersion));
        Assert.False(string.IsNullOrWhiteSpace(policy.LatestWebVersion));
    }

    [Fact]
    public async Task Manifest_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/content/manifest");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RealtimeHub_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/events/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetupLoginAndManifest_WorkEndToEnd()
    {
        var client = _factory.CreateClient();

        var tokens = await SetupAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var manifestResponse = await client.GetAsync("/v1/content/manifest");
        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);

        var manifest = await manifestResponse.Content.ReadFromJsonAsync<ContentManifestDto>(JsonOptions);
        Assert.NotNull(manifest);
        Assert.False(string.IsNullOrWhiteSpace(manifest!.Version));
        Assert.NotEmpty(manifest.Categories);

        var refresh = await client.PostAsJsonAsync(
            "/v1/auth/refresh",
            new RefreshRequest(tokens.RefreshToken),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
        Assert.NotNull(refreshed);
        Assert.NotEqual(tokens.RefreshToken, refreshed!.RefreshToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

        var kindResponse = await client.GetAsync(
            $"/v1/reports/kind/Summary?from={DateTime.Today:yyyy-MM-dd}&to={DateTime.Today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, kindResponse.StatusCode);

        var kindPayload = await kindResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Summary", kindPayload.GetProperty("kind").GetString());
        Assert.True(kindPayload.TryGetProperty("summary", out var summaryPayload));
        Assert.True(summaryPayload.TryGetProperty("total", out _));

        var flatSummary = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/reports/summary?from={DateTime.Today:yyyy-MM-dd}&to={DateTime.Today:yyyy-MM-dd}",
            JsonOptions);
        Assert.True(flatSummary.TryGetProperty("total", out _));
        Assert.False(flatSummary.TryGetProperty("summary", out _));

        var negotiate = await client.PostAsync("/hubs/events/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);

        var negotiatePayload = await negotiate.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(negotiatePayload.GetProperty("connectionToken").GetString()));

        var publicKey = await client.GetFromJsonAsync<JsonElement>("/v1/push/public-key", JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(publicKey.GetProperty("publicKey").GetString()));

        var registration = new DeviceRegistrationRequest(
            "web",
            Endpoint: "https://example.invalid/push/1",
            P256dh: "p256dh",
            Auth: "auth",
            AppVersion: "test");

        var register = await client.PostAsJsonAsync("/v1/devices", registration, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var devices = await client.GetFromJsonAsync<List<DeviceDto>>("/v1/devices", JsonOptions);
        Assert.NotNull(devices);
        Assert.Contains(devices!, device => device.Platform == "web");

        var delete = await client.DeleteAsync("/v1/devices?endpoint=https%3A%2F%2Fexample.invalid%2Fpush%2F1");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    private static async Task<AuthTokensResponse> SetupAsync(HttpClient client)
    {
        var status = await client.GetFromJsonAsync<JsonElement>("/v1/auth/status", JsonOptions);
        Assert.True(status.GetProperty("isFirstRun").GetBoolean());

        var response = await client.PostAsJsonAsync(
            "/v1/auth/setup",
            new SetupRequestDto(
                "admin",
                "Test Yönetici",
                "admin@example.com",
                "Admin12345",
                "Admin12345",
                "api-test"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
        Assert.NotNull(tokens);
        return tokens!;
    }
}
