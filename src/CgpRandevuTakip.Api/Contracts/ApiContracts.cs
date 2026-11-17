using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;

namespace CgpRandevuTakip.Api.Contracts;

public sealed record LoginRequest(string Username, string Password, string? DeviceName);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserDto(string Id, string Username, string DisplayName, string Role);

public sealed record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    UserDto User);

public sealed record SetupRequestDto(
    string Username,
    string DisplayName,
    string Email,
    string Password,
    string ConfirmPassword,
    string? DeviceName);

public sealed record InstitutionDto(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl);

public sealed record PublicSettingsDto(
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

public sealed record VersionPolicyDto(
    string MinDesktopVersion,
    string MinMobileVersion,
    string MinWebVersion,
    string LatestDesktopVersion,
    string LatestMobileVersion);

public sealed record ContentManifestDto(
    string Version,
    DateTime GeneratedAtUtc,
    InstitutionDto Institution,
    PublicSettingsDto Settings,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<HostDto> Hosts,
    IReadOnlyDictionary<string, bool> Features,
    VersionPolicyDto VersionPolicy);

public sealed record ErrorResponse(string Error, IReadOnlyList<string>? Details = null);

public sealed record DeviceRegistrationRequest(
    string Platform,
    string? Token = null,
    string? Endpoint = null,
    string? P256dh = null,
    string? Auth = null,
    string? AppVersion = null);

public sealed record DeviceDto(string Id, string Platform, string? AppVersion, DateTime UpdatedAtUtc);

public sealed record PushPublicKeyResponse(string PublicKey, string Subject);

public sealed record PushTestResponse(int Sent, int DeviceCount);
