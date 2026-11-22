using System.Net;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Rooms;
using CgpRandevuTakip.Application.Titles;

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

/// <summary>İçerik manifestindeki kurum bilgisi.</summary>
public sealed record ApiInstitution(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl);

/// <summary>İçerik manifestindeki istemciye açık ayarlar.</summary>
public sealed record ApiPublicSettings(
    string Theme,
    int DefaultReminderMinutes,
    string WorkHoursStart,
    string WorkHoursEnd,
    bool AllowOutOfHoursAppointments,
    bool NotificationsEnabled,
    bool PushEnabled,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd,
    string DailyShareTime);

/// <summary>Sunucunun sürüm politikası.</summary>
public sealed record ApiVersionPolicy(
    string MinDesktopVersion,
    string MinMobileVersion,
    string MinWebVersion,
    string LatestDesktopVersion,
    string LatestMobileVersion,
    string LatestWebVersion);

/// <summary>İçerik manifesti (<c>GET /v1/content/manifest</c>).</summary>
public sealed record ApiManifest(
    string Version,
    DateTime GeneratedAtUtc,
    ApiInstitution Institution,
    ApiPublicSettings Settings,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<HostDto> Hosts,
    IReadOnlyDictionary<string, bool> Features,
    ApiVersionPolicy VersionPolicy,
    IReadOnlyList<HostTitleDto>? Titles = null,
    IReadOnlyList<MeetingRoomDto>? Rooms = null);

/// <summary>İstemci sonucu; ağ hatalarında <see cref="StatusCode"/> 0 olur.</summary>
public sealed record ApiResult<T>(bool Success, T? Data, string? Error, int StatusCode)
{
    /// <summary>Sunucunun döndürdüğü ETag (varsa).</summary>
    public string? ETag { get; init; }

    /// <summary>Koşullu istek 304 döndürdü; veri önbellekten okunmalı.</summary>
    public bool NotModified { get; init; }

    public bool IsUnauthorized => StatusCode == (int)HttpStatusCode.Unauthorized;

    public bool IsNetworkError => StatusCode == 0;

    public static ApiResult<T> Ok(T data, int statusCode = 200, string? etag = null)
        => new(true, data, null, statusCode) { ETag = etag };

    /// <summary>Gövdesiz başarılı yanıt (204 gibi).</summary>
    public static ApiResult<T> Empty(int statusCode = 204)
        => new(true, default, null, statusCode);

    public static ApiResult<T> NotModifiedResult(string? etag)
        => new(true, default, null, (int)HttpStatusCode.NotModified) { NotModified = true, ETag = etag };

    public static ApiResult<T> Fail(string error, int statusCode = 0)
        => new(false, default, error, statusCode);
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
