using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Desktop.Services.Api;

/// <summary>
/// Sunucu adresi ve JWT oturumunu yerel ayarlarda saklar. Token'lar
/// <see cref="ISecretProtector"/> (DPAPI) ile şifrelenir; düz metin yazılmaz.
/// </summary>
public sealed class ApiSessionStore
{
    public const string DefaultBaseUrl = "http://127.0.0.1:5199";

    private const string BaseUrlKey = "sync.api.base_url";
    private const string AccessTokenKey = "sync.api.access_token";
    private const string RefreshTokenKey = "sync.api.refresh_token";
    private const string ExpiresAtKey = "sync.api.expires_at";
    private const string UserKey = "sync.api.user";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISettingsRepository _settings;
    private readonly ISecretProtector _protector;

    public ApiSessionStore(ISettingsRepository settings, ISecretProtector protector)
    {
        _settings = settings;
        _protector = protector;
    }

    public async Task<string> GetBaseUrlAsync()
    {
        var values = await _settings.GetAllAsync();
        return values.TryGetValue(BaseUrlKey, out var stored) && !string.IsNullOrWhiteSpace(stored)
            ? NormalizeBaseUrl(stored)
            : DefaultBaseUrl;
    }

    public async Task SetBaseUrlAsync(string baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        await _settings.SetManyAsync(new Dictionary<string, string> { [BaseUrlKey] = normalized });
    }

    public async Task<ApiSession?> LoadAsync()
    {
        var values = await _settings.GetAllAsync();
        var baseUrl = values.TryGetValue(BaseUrlKey, out var storedUrl) && !string.IsNullOrWhiteSpace(storedUrl)
            ? NormalizeBaseUrl(storedUrl)
            : DefaultBaseUrl;

        if (!values.TryGetValue(AccessTokenKey, out var protectedAccess)
            || !values.TryGetValue(RefreshTokenKey, out var protectedRefresh)
            || !values.TryGetValue(ExpiresAtKey, out var expiresText)
            || !values.TryGetValue(UserKey, out var userJson))
        {
            return null;
        }

        var accessToken = _protector.Unprotect(protectedAccess);
        var refreshToken = _protector.Unprotect(protectedRefresh);
        if (string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(refreshToken)
            || !DateTimeOffset.TryParse(expiresText, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt))
        {
            return null;
        }

        ApiUser? user;
        try
        {
            user = JsonSerializer.Deserialize<ApiUser>(userJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        return user is null ? null : new ApiSession(baseUrl, accessToken, refreshToken, expiresAt, user);
    }

    public async Task SaveAsync(ApiSession session)
    {
        var normalized = NormalizeBaseUrl(session.BaseUrl);
        await _settings.SetManyAsync(new Dictionary<string, string>
        {
            [BaseUrlKey] = normalized,
            [AccessTokenKey] = _protector.Protect(session.AccessToken) ?? string.Empty,
            [RefreshTokenKey] = _protector.Protect(session.RefreshToken) ?? string.Empty,
            [ExpiresAtKey] = session.ExpiresAtUtc.ToString("O"),
            [UserKey] = JsonSerializer.Serialize(session.User, JsonOptions),
        });
    }

    public async Task ClearTokensAsync()
    {
        await _settings.SetManyAsync(new Dictionary<string, string>
        {
            [AccessTokenKey] = string.Empty,
            [RefreshTokenKey] = string.Empty,
            [ExpiresAtKey] = string.Empty,
            [UserKey] = string.Empty,
        });
    }

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        var candidate = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Sunucu adresi http:// veya https:// ile başlamalıdır.", nameof(baseUrl));
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
