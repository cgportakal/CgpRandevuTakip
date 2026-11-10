using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Notifications;
using CgpRandevuTakip.Application.Reminders;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.ViewModels;
using CgpRandevuTakip.Desktop.Views;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Zamanı gelen hatırlatıcıları işleyen arka plan servisi. Windows kanalı (uygulama içi
/// topmost toast), push kanalı ve aksiyon penceresi burada yönetilir.
/// </summary>
public sealed class ReminderDispatcher : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMinutes(10);
    private const int MaxRepeats = 3; // ilk gösterim + 3 tekrar = toplam 4
    private const int BatchSize = 50;

    private readonly IReminderService _reminders;
    private readonly ISettingsService _settings;
    private readonly ICurrentUser _currentUser;
    private readonly SessionStateService _session;
    private readonly IClock _clock;
    private readonly IPushNotifier _push;
    private readonly WindowsToastService _windowsToast;
    private readonly IDialogService _dialogs;

    private readonly ConcurrentDictionary<string, ReminderActionWindow> _openWindows = new();
    private readonly ConcurrentDictionary<string, ReminderContextDto> _recentContexts = new();

    public ReminderDispatcher(
        IReminderService reminders,
        ISettingsService settings,
        ICurrentUser currentUser,
        SessionStateService session,
        IClock clock,
        IPushNotifier push,
        WindowsToastService windowsToast,
        IDialogService dialogs)
    {
        _reminders = reminders;
        _settings = settings;
        _currentUser = currentUser;
        _session = session;
        _clock = clock;
        _push = push;
        _windowsToast = windowsToast;
        _dialogs = dialogs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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
                // Bildirim hatası uygulamayı durdurmaz; sonraki tick'te yeniden denenir.
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task TickAsync()
    {
        var nowUtc = _clock.UtcNow;

        await _reminders.RequeueExpiredAsync(nowUtc, RepeatDelay, MaxRepeats);

        var due = await _reminders.GetDueAsync(nowUtc, BatchSize);
        if (due.Count == 0)
            return;

        var settings = await _settings.GetAsync();
        if (!settings.NotificationsEnabled)
            return;

        var quiet = IsQuietNow(nowUtc, settings);

        foreach (var context in due)
        {
            await ProcessAsync(context, settings, quiet, nowUtc);
        }
    }

    private async Task ProcessAsync(
        ReminderContextDto context,
        AppSettings settings,
        bool quiet,
        DateTime nowUtc)
    {
        // Randevu geçmişse bildirim gösterilmez; hatırlatıcı "kaçırıldı" olarak işaretlenir.
        if (context.IsOverdue)
        {
            await _reminders.MarkMissedAsync(context.ReminderId, nowUtc);
            return;
        }

        _recentContexts[context.ReminderId] = context;

        // Windows kanalı.
        if (!context.WindowsNotified)
        {
            await OnUiAsync(() => ShowToast(context, quiet));
            await _reminders.MarkWindowsNotifiedAsync(context.ReminderId, nowUtc);
        }

        // Push kanalı (sessiz saatte gönderilmez).
        if (settings.PushEnabled && !context.PushNotified && !quiet)
        {
            var sent = await _push.SendAsync(new PushRequest(
                settings.PushProvider,
                settings.PushTopic,
                settings.PushToken,
                settings.PushChatId,
                context.Kind == Domain.Enums.ReminderKind.Call
                    ? "Sıradaki kişiyi arama zamanı"
                    : "Sıradaki görüşme yaklaşıyor",
                context.NotificationBody));

            if (sent)
                await _reminders.MarkPushNotifiedAsync(context.ReminderId, nowUtc);
        }

        // Aksiyon penceresi yalnızca kimlik doğrulanmış ve kilitli değilken açılır.
        if (!_currentUser.IsAuthenticated || _session.IsLocked)
            return;

        if (_openWindows.ContainsKey(context.ReminderId))
            return;

        await ShowActionWindowAsync(context);

        await _reminders.MarkSentAsync(context.ReminderId);
    }

    /// <summary>Toast tıklamasında ilgili hatırlatıcı penceresini açar.</summary>
    public async Task<bool> ActivateAsync(string reminderId)
    {
        if (_openWindows.TryGetValue(reminderId, out var openWindow))
        {
            await OnUiAsync(() =>
            {
                if (openWindow.IsVisible)
                    openWindow.Activate();
            });
            return true;
        }

        if (!_recentContexts.TryGetValue(reminderId, out var context))
            return false;

        if (!_currentUser.IsAuthenticated || _session.IsLocked)
            return false;

        await ShowActionWindowAsync(context);
        return true;
    }

    private async Task ShowActionWindowAsync(ReminderContextDto context)
    {
        await OnUiAsync(() =>
        {
            var viewModel = new ReminderActionViewModel(_reminders, _clock, _dialogs, context);
            var window = new ReminderActionWindow(viewModel)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            _openWindows[context.ReminderId] = window;
            window.Closed += (_, _) => _openWindows.TryRemove(context.ReminderId, out _);
            window.Show();
        });
    }

    private void ShowToast(ReminderContextDto context, bool silent)
    {
        var title = context.Kind == Domain.Enums.ReminderKind.Call
            ? "Sıradaki kişiyi arama zamanı"
            : "Sıradaki görüşme yaklaşıyor";

        if (_windowsToast.TryShow(title, context.NotificationBody, silent, context.ReminderId))
            return;

        var window = new ReminderToastWindow(title, context.NotificationBody);
        window.ShowNotification();
    }

    private static bool IsQuietNow(DateTime nowUtc, AppSettings settings)
    {
        if (!settings.QuietHoursEnabled)
            return false;

        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local);
        var start = TimeSpan.TryParse(settings.QuietHoursStart, out var s) ? s : TimeSpan.FromHours(22);
        var end = TimeSpan.TryParse(settings.QuietHoursEnd, out var e) ? e : TimeSpan.FromHours(7);

        return NotificationPolicy.IsQuietNow(local, start, end, enabled: true);
    }

    private static async Task OnUiAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
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
