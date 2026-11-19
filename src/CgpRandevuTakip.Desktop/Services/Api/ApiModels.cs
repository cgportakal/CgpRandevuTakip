using System.Net;

namespace CgpRandevuTakip.Desktop.Services.Api;

/// <summary>API kullanıcısı; sunucu tarafındaki rol adını taşır.</summary>
public sealed record ApiUser(string Id, string Username, string DisplayName, string Role);

/// <summary>Giriş/yenileme yanıtı.</summary>
public sealed record ApiAuthTokens(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    ApiUser User);

/// <summary>Sağlık ucu yanıtı.</summary>
public sealed record ApiHealth(string Status, string Version, int SchemaVersion, DateTime ServerTimeUtc);

/// <summary>API hata gövdesi.</summary>
public sealed record ApiError(string? Error);

/// <summary>İstemci sonucu; ağ hatalarında <see cref="StatusCode"/> 0 olur.</summary>
public sealed record ApiResult<T>(bool Success, T? Data, string? Error, int StatusCode)
{
    public static ApiResult<T> Ok(T data, int statusCode = 200)
        => new(true, data, null, statusCode);

    public static ApiResult<T> Fail(string error, int statusCode = 0)
        => new(false, default, error, statusCode);

    public bool IsUnauthorized => StatusCode == (int)HttpStatusCode.Unauthorized;
}

/// <summary>Kalıcı sunucu oturumu; token'lar DPAPI ile şifrelenmiş saklanır.</summary>
public sealed record ApiSession(
    string BaseUrl,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    ApiUser User)
{
    public bool IsExpiring(DateTimeOffset now) => ExpiresAtUtc <= now.AddMinutes(1);
}
