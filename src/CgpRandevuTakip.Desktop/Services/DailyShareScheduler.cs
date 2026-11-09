using System.Windows;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Views;
using CgpRandevuTakip.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Günlük paylaşım saatinde paylaşım penceresini açar; saat kaçırıldıysa tepsi
/// balonuyla hatırlatır. Aynı gün için yalnızca bir kez tetiklenir.
/// </summary>
public sealed class DailyShareScheduler : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LateWindow = TimeSpan.FromMinutes(60);

    private readonly ISettingsService _settings;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IShareLogRepository _logs;
    private readonly IServiceProvider _services;
    private readonly TrayService _tray;

    private DateOnly? _promptedDate;

    public DailyShareScheduler(
        ISettingsService settings,
        IClock clock,
        ICurrentUser currentUser,
        IShareLogRepository logs,
        IServiceProvider services,
        TrayService tray)
    {
        _settings = settings;
        _clock = clock;
        _currentUser = currentUser;
        _logs = logs;
        _services = services;
        _tray = tray;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
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
                await TickAsync();
            }
            catch
            {
                // Paylaşım hatırlatması uygulamayı durdurmaz.
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task TickAsync()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TimeZoneInfo.Local);
        var today = DateOnly.FromDateTime(localNow);

        if (_promptedDate == today)
            return;

        var settings = await _settings.GetAsync();
        if (!TimeSpan.TryParse(settings.DailyShareTime, out var shareTime))
            return;

        if (localNow.TimeOfDay < shareTime)
            return;

        var recent = await _logs.GetRecentAsync(20);
        var sharedToday = recent.Any(x =>
            x.Method == ShareMethod.DailyList
            && DateOnly.FromDateTime(x.ShareDate) == today
            && x.Result != ShareResult.Failed);

        if (sharedToday)
        {
            _promptedDate = today;
            return;
        }

        if (localNow.TimeOfDay > shareTime + LateWindow)
        {
            _promptedDate = today;
            _tray.ShowBalloon(
                "Günlük liste paylaşılmadı",
                "Paylaşım saatini kaçırdınız. Günlük Liste > Paylaş ile hazırlayabilirsiniz.");
            return;
        }

        if (!_currentUser.IsAuthenticated)
            return;

        _promptedDate = today;

        var application = System.Windows.Application.Current;
        if (application is null)
            return;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = application.Dispatcher.Invoke(async () =>
        {
            try
            {
                var window = _services.GetRequiredService<ShareDailyListWindow>();
                window.Owner = application.MainWindow;
                await window.InitializeAsync(localNow.Date);
                window.ShowDialog();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        await completion.Task;
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
}
