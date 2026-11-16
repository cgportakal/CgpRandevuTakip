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
    public async Task Manifest_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/content/manifest");

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
