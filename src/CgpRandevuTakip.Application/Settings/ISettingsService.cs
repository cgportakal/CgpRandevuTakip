using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Settings;

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task<OperationResult> SaveAsync(AppSettings settings);
    Task<OperationResult> SetThemeAsync(AppTheme theme);

    /// <summary>Önbelleği temizler; geri yükleme sonrası ayarların yeniden okunması için.</summary>
    void InvalidateCache();

    IReadOnlyList<int> AllowedReminderMinutes { get; }
}
