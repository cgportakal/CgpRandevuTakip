using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Settings;

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task<OperationResult> SaveAsync(AppSettings settings);
    Task<OperationResult> SetThemeAsync(AppTheme theme);
    IReadOnlyList<int> AllowedReminderMinutes { get; }
}
