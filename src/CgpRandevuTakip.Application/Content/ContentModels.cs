using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Content;

public sealed record ContentInstitutionDto(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl);

public sealed record ContentSettingsDto(
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

public sealed record ContentVersionDto(
    string Id,
    int VersionNumber,
    DateTime PublishedAtLocal,
    string PublishedBy,
    string? Note,
    int CategoryCount,
    int HostCount,
    bool IsCurrent);

public sealed record ContentAdminDto(
    ContentInstitutionDto Institution,
    ContentSettingsDto Settings,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<HostDto> Hosts,
    ContentVersionDto? CurrentVersion,
    IReadOnlyList<ContentVersionDto> Versions);

public sealed record ContentSettingsInput(
    string InstitutionName,
    string InstitutionEmail,
    string InstitutionPhone,
    string InstitutionAddress,
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

public sealed record ContentPublishResult(
    bool Success,
    string? Error,
    ContentVersionDto? Version)
{
    public static ContentPublishResult Ok(ContentVersionDto version) => new(true, null, version);
    public static ContentPublishResult Fail(string error) => new(false, error, null);
}

public sealed record ContentSnapshotDto(
    ContentInstitutionDto Institution,
    ContentSettingsDto Settings,
    IReadOnlyList<ContentCategorySnapshot> Categories,
    IReadOnlyList<ContentHostSnapshot> Hosts);

public sealed record ContentCategorySnapshot(
    string Id,
    string Name,
    string ColorHex,
    int SortOrder,
    bool IsActive);

public sealed record ContentHostSnapshot(
    string Id,
    string DisplayName,
    UserRole Role,
    bool IsActive);

public sealed record PublishContentRequest(string? Note);

public interface IContentAdminService
{
    Task<ContentAdminDto> GetAsync();
    Task<OperationResult> SaveSettingsAsync(ContentSettingsInput input);
    Task<ContentPublishResult> PublishAsync(string? note);
    Task<ContentPublishResult> RestoreAsync(string versionId);
}
