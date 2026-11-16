using CgpRandevuTakip.Api.Notifications;
using CgpRandevuTakip.Api.Realtime;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Notifications;
using CgpRandevuTakip.Application.Reminders;
using CgpRandevuTakip.Application.Settings;
using Microsoft.Extensions.Options;

namespace CgpRandevuTakip.Api.Realtime;

/// <summary>
/// Sunucu tarafı hatırlatma işleyicisi. Web/mobil istemciler uygulama kapalıyken de
/// hatırlatma alabilsin diye SignalR olayı ve ntfy/Telegram push üretir.
/// Masaüstü kendi yerel veritabanıyla çalıştığı için bu servisten etkilenmez.
/// </summary>
public sealed class ReminderWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMinutes(10);
    private const int MaxRepeats = 3;
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PushOptions> _pushOptions;
    private readonly ILogger<ReminderWorker> _logger;

    public ReminderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PushOptions> pushOptions,
        ILogger<ReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _pushOptions = pushOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation("Sunucu hatırlatma işleyicisi başladı.");

        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Hatırlatma turu başarısız; sonraki turda yeniden denenecek.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotifier>();
        var devicePush = scope.ServiceProvider.GetRequiredService<IDevicePushSender>();

        var nowUtc = clock.UtcNow;
        await reminders.RequeueExpiredAsync(nowUtc, RepeatDelay, MaxRepeats);

        var due = await reminders.GetDueAsync(nowUtc, BatchSize);
        if (due.Count == 0)
            return;

        _logger.LogInformation("Hatırlatma turu: {Count} kayıt işleniyor.", due.Count);

        var settings = await settingsService.GetAsync();
        if (!settings.NotificationsEnabled)
        {
            _logger.LogInformation("Bildirimler kapalı; hatırlatmalar yayınlanmadı.");
            return;
        }

        var quiet = IsQuietNow(nowUtc, settings);

        foreach (var context in due)
        {
            if (context.IsOverdue)
            {
                await reminders.MarkMissedAsync(context.ReminderId, nowUtc);
                continue;
            }

            // Uygulama açık istemciler için anlık olay (sessiz saatte sessiz gösterilir).
            await notifier.ReminderDueAsync(context, quiet);
            _logger.LogInformation(
                "Hatırlatma yayınlandı: {ReminderId} {Person} {Time}",
                context.ReminderId,
                context.PersonFullName,
                context.TimeLabel);

            if (!context.PushNotified && !quiet)
            {
                var delivered = false;

                var request = BuildPushRequest(context, settings);
                if (request is not null && await push.SendAsync(request, cancellationToken))
                    delivered = true;

                var title = context.Kind == Domain.Enums.ReminderKind.Call
                    ? "Sıradaki kişiyi arama zamanı"
                    : "Sıradaki görüşme yaklaşıyor";

                var data = new Dictionary<string, string>
                {
                    ["type"] = "reminder",
                    ["reminderId"] = context.ReminderId,
                    ["appointmentId"] = context.AppointmentId
                };

                if (await devicePush.SendAsync(title, context.NotificationBody, data, cancellationToken) > 0)
                    delivered = true;

                if (delivered)
                    await reminders.MarkPushNotifiedAsync(context.ReminderId, nowUtc);
            }

            await reminders.MarkSentAsync(context.ReminderId);
        }
    }

    private PushRequest? BuildPushRequest(ReminderContextDto context, AppSettings settings)
    {
        var options = _pushOptions.Value;

        var provider = options.Enabled ? options.Provider : settings.PushProvider;
        var topic = options.Enabled ? options.Topic : settings.PushTopic;
        var token = options.Enabled ? options.Token : settings.PushToken;
        var chatId = options.Enabled ? options.ChatId : settings.PushChatId;

        if (!options.Enabled && !settings.PushEnabled)
            return null;

        var title = context.Kind == Domain.Enums.ReminderKind.Call
            ? "Sıradaki kişiyi arama zamanı"
            : "Sıradaki görüşme yaklaşıyor";

        return new PushRequest(provider, topic, token, chatId, title, context.NotificationBody);
    }

    private static bool IsQuietNow(DateTime nowUtc, AppSettings settings)
    {
        if (!settings.QuietHoursEnabled)
            return false;

        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local);
        var start = TimeSpan.TryParse(settings.QuietHoursStart, out var parsedStart)
            ? parsedStart
            : TimeSpan.FromHours(22);
        var end = TimeSpan.TryParse(settings.QuietHoursEnd, out var parsedEnd)
            ? parsedEnd
            : TimeSpan.FromHours(7);

        return NotificationPolicy.IsQuietNow(local, start, end, enabled: true);
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
