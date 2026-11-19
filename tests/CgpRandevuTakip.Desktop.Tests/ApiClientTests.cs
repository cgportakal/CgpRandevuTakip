using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Desktop.Services.Api;

namespace CgpRandevuTakip.Desktop.Tests;

public class ApiClientTests
{
    [Fact]
    public async Task LoginAsync_Success_ReturnsTokens()
    {
        string? sentBody = null;
        using var http = new HttpClient(new StubHandler(async (request, _) =>
        {
            sentBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, new
            {
                accessToken = "access-1",
                refreshToken = "refresh-1",
                expiresInSeconds = 3600,
                user = new { id = "u1", username = "admin", displayName = "Yönetici", role = "Admin" },
            });
        }));

        var result = await new CgpApiClient(http).LoginAsync("http://localhost:5199", "admin", "Admin12345");

        Assert.True(result.Success);
        Assert.Equal("access-1", result.Data!.AccessToken);
        Assert.Equal("Yönetici", result.Data.User.DisplayName);
        Assert.Contains("\"deviceName\":\"CGP-Desktop\"", sentBody);
    }

    [Fact]
    public async Task LoginAsync_Unauthorized_ReturnsApiError()
    {
        using var http = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(Json(HttpStatusCode.Unauthorized, new { error = "Kullanıcı adı veya şifre hatalı." }))));

        var result = await new CgpApiClient(http).LoginAsync("http://localhost:5199", "admin", "yanlis");

        Assert.False(result.Success);
        Assert.True(result.IsUnauthorized);
        Assert.Equal("Kullanıcı adı veya şifre hatalı.", result.Error);
    }

    [Fact]
    public async Task TestAsync_WhenHealthy_SavesNormalizedBaseUrl()
    {
        var settings = new FakeSettingsRepository();
        var store = new ApiSessionStore(settings, new FakeProtector());
        using var http = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, new
            {
                status = "ok",
                version = "1.8.3",
                schemaVersion = 1,
                serverTimeUtc = DateTime.UtcNow,
            }))));

        var service = new ApiConnectionService(new CgpApiClient(http), store);
        var result = await service.TestAsync("http://localhost:5199/");

        Assert.True(result.Success);
        Assert.Equal("1.8.3", result.Data!.Version);
        Assert.Equal("http://localhost:5199", await store.GetBaseUrlAsync());
    }

    [Fact]
    public async Task SignInAsync_SavesSession()
    {
        var settings = new FakeSettingsRepository();
        var store = new ApiSessionStore(settings, new FakeProtector());
        using var http = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, new
            {
                accessToken = "access-2",
                refreshToken = "refresh-2",
                expiresInSeconds = 3600,
                user = new { id = "u1", username = "admin", displayName = "Yönetici", role = "Admin" },
            }))));

        var service = new ApiConnectionService(new CgpApiClient(http), store);
        var result = await service.SignInAsync("http://localhost:5199", "admin", "Admin12345");

        Assert.True(result.Success);
        var session = await store.LoadAsync();
        Assert.NotNull(session);
        Assert.Equal("access-2", session!.AccessToken);
        Assert.Equal("http://localhost:5199", session.BaseUrl);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenTokenExpiring_RefreshesAndRetries()
    {
        var settings = new FakeSettingsRepository();
        var store = new ApiSessionStore(settings, new FakeProtector());
        await store.SaveAsync(new ApiSession(
            "http://localhost:5199",
            "old-access",
            "old-refresh",
            DateTimeOffset.UtcNow.AddSeconds(10),
            new ApiUser("u1", "admin", "Yönetici", "Admin")));

        var calls = new List<string>();
        using var http = new HttpClient(new StubHandler((request, _) =>
        {
            calls.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return request.RequestUri!.AbsolutePath switch
            {
                "/v1/auth/refresh" => Task.FromResult(Json(HttpStatusCode.OK, new
                {
                    accessToken = "new-access",
                    refreshToken = "new-refresh",
                    expiresInSeconds = 3600,
                    user = new { id = "u1", username = "admin", displayName = "Yönetici", role = "Admin" },
                })),
                "/v1/auth/me" => Task.FromResult(Json(HttpStatusCode.OK,
                    new { id = "u1", username = "admin", displayName = "Yönetici", role = "Admin" })),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
            };
        }));

        var service = new ApiConnectionService(new CgpApiClient(http), store);
        var result = await service.GetCurrentUserAsync();

        Assert.True(result.Success);
        Assert.Contains("POST /v1/auth/refresh", calls);
        Assert.Equal("new-access", (await store.LoadAsync())!.AccessToken);
    }

    [Fact]
    public async Task GetCurrentUserAsync_On401_RefreshesAndRetriesOnce()
    {
        var settings = new FakeSettingsRepository();
        var store = new ApiSessionStore(settings, new FakeProtector());
        await store.SaveAsync(new ApiSession(
            "http://localhost:5199",
            "stale-access",
            "refresh-1",
            DateTimeOffset.UtcNow.AddHours(1),
            new ApiUser("u1", "admin", "Yönetici", "Admin")));

        var meCalls = 0;
        using var http = new HttpClient(new StubHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/auth/refresh")
            {
                return Task.FromResult(Json(HttpStatusCode.OK, new
                {
                    accessToken = "fresh-access",
                    refreshToken = "refresh-2",
                    expiresInSeconds = 3600,
                    user = new { id = "u1", username = "admin", displayName = "Yönetici", role = "Admin" },
                }));
            }

            meCalls++;
            var token = request.Headers.Authorization?.Parameter;
            return Task.FromResult(token == "fresh-access"
                ? Json(HttpStatusCode.OK, new { id = "u1", username = "admin", displayName = "Yönetici", role = "Admin" })
                : Json(HttpStatusCode.Unauthorized, new { error = "Oturum süresi doldu." }));
        }));

        var result = await new ApiConnectionService(new CgpApiClient(http), store).GetCurrentUserAsync();

        Assert.True(result.Success);
        Assert.Equal(2, meCalls);
        Assert.Equal("fresh-access", (await store.LoadAsync())!.AccessToken);
    }

    [Fact]
    public async Task SessionStore_RoundTripsAndClearTokensKeepsBaseUrl()
    {
        var settings = new FakeSettingsRepository();
        var store = new ApiSessionStore(settings, new FakeProtector());
        var session = new ApiSession(
            "https://cgp.local/",
            "access",
            "refresh",
            DateTimeOffset.UtcNow.AddHours(2),
            new ApiUser("u1", "admin", "Yönetici", "Admin"));

        await store.SaveAsync(session);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("https://cgp.local", loaded!.BaseUrl);
        Assert.Equal("access", loaded.AccessToken);
        Assert.Equal("Admin", loaded.User.Role);

        await store.ClearTokensAsync();
        Assert.Null(await store.LoadAsync());
        Assert.Equal("https://cgp.local", await store.GetBaseUrlAsync());
    }

    [Theory]
    [InlineData("http://localhost:5199/", "http://localhost:5199")]
    [InlineData("https://cgp.local/api/", "https://cgp.local/api")]
    [InlineData("  http://127.0.0.1:5199  ", "http://127.0.0.1:5199")]
    public void NormalizeBaseUrl_TrimsAndValidates(string input, string expected)
    {
        Assert.Equal(expected, ApiSessionStore.NormalizeBaseUrl(input));
    }

    [Theory]
    [InlineData("localhost:5199")]
    [InlineData("ftp://cgp.local")]
    [InlineData("")]
    public void NormalizeBaseUrl_Invalid_Throws(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Assert.Equal(ApiSessionStore.DefaultBaseUrl, ApiSessionStore.NormalizeBaseUrl(input));
            return;
        }

        Assert.Throws<ArgumentException>(() => ApiSessionStore.NormalizeBaseUrl(input));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
        => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync()
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_values));

        public Task SetManyAsync(IReadOnlyDictionary<string, string> values)
        {
            foreach (var (key, value) in values) _values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProtector : ISecretProtector
    {
        private const string Prefix = "enc:";

        public string? Protect(string? plainText)
            => string.IsNullOrEmpty(plainText) ? plainText : Prefix + plainText;

        public string? Unprotect(string? cipherText)
            => cipherText is not null && cipherText.StartsWith(Prefix, StringComparison.Ordinal)
                ? cipherText[Prefix.Length..]
                : cipherText;
    }
}
