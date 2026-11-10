using System.IO;
using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Günde iki otomatik yedek alır:
/// 1) Öğle yedeği (12:30),
/// 2) Günlük liste öğleden sonra paylaşıldığında hemen; paylaşım yoksa 18:00'de.
/// Aynı gün için en fazla 2 otomatik yedek oluşturur.
/// </summary>
public sealed class AutoBackupService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly ISettingsService _settings;
    private readonly IBackupService _backups;
    private readonly IShareLogRepository _logs;
    private readonly IClock _clock;
    private readonly string _statePath = Path.Combine(AppPaths.Root, "auto-backup-state.json");

    public AutoBackupService(
        ISettingsService settings,
        IBackupService backups,
        IShareLogRepository logs,
        IClock clock)
    {
        _settings = settings;
        _backups = backups;
        _logs = logs;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch
            {
                // Yedek hatası uygulamayı durdurmaz; sonraki dakika yeniden denenir.
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync();
        if (!settings.AutoBackupEnabled)
            return;

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TimeZoneInfo.Local);
        var today = DateOnly.FromDateTime(localNow);
        var state = LoadState(today);

        if (state.Date != today.ToString("yyyy-MM-dd"))
            state = new AutoBackupState(today.ToString("yyyy-MM-dd"), 0);

        if (state.Count >= 2)
            return;

        var sharedAfterNoon = await HasAfternoonShareAsync(localNow, cancellationToken);
        if (!AutoBackupSchedule.ShouldRun(localNow, state.Count, sharedAfterNoon))
            return;

        var result = await _backups.CreateAsync(BackupTrigger.Automatic, cancellationToken);
        if (!result.Success)
            return;

        state = state with { Count = state.Count + 1 };
        SaveState(state);
    }

    private async Task<bool> HasAfternoonShareAsync(DateTime localNow, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var logs = await _logs.GetRecentAsync(50);
        return logs.Any(x =>
            x.Method == ShareMethod.DailyList
            && x.Result != ShareResult.Failed
            && TimeZoneInfo.ConvertTimeFromUtc(x.CreatedAtUtc, TimeZoneInfo.Local).Date == localNow.Date
            && TimeZoneInfo.ConvertTimeFromUtc(x.CreatedAtUtc, TimeZoneInfo.Local).TimeOfDay >= AutoBackupSchedule.Noon);
    }

    private AutoBackupState LoadState(DateOnly today)
    {
        try
        {
            if (!File.Exists(_statePath))
                return new AutoBackupState(today.ToString("yyyy-MM-dd"), 0);

            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<AutoBackupState>(json)
                   ?? new AutoBackupState(today.ToString("yyyy-MM-dd"), 0);
        }
        catch
        {
            return new AutoBackupState(today.ToString("yyyy-MM-dd"), 0);
        }
    }

    private void SaveState(AutoBackupState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(state));
        }
        catch
        {
            // Durum yazılamazsa bir sonraki açılışta yedek yeniden denenebilir.
        }
    }

    private static async Task<bool> WaitForNextTickAsync(
        PeriodicTimer timer,
        CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private sealed record AutoBackupState(string Date, int Count);
}
