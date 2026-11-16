using CgpRandevuTakip.Application.Reminders;
using Microsoft.AspNetCore.SignalR;

namespace CgpRandevuTakip.Api.Realtime;

public interface IRealtimeNotifier
{
    Task AppointmentChangedAsync(string appointmentId, string change, DateTime startsAtLocal);
    Task SeriesChangedAsync(string seriesId, string change);
    Task ReminderDueAsync(ReminderContextDto context, bool silent);
}

/// <summary>Uygulama servislerinden gelen değişiklikleri bağlı istemcilere yayınlar.</summary>
public sealed class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationsHub> _hub;

    public RealtimeNotifier(IHubContext<NotificationsHub> hub) => _hub = hub;

    public Task AppointmentChangedAsync(string appointmentId, string change, DateTime startsAtLocal)
        => _hub.Clients
            .Group(NotificationsHub.InstitutionGroup)
            .SendAsync("appointmentChanged", new
            {
                appointmentId,
                change,
                dateKey = startsAtLocal.ToString("yyyy-MM-dd"),
                startsAtLocal,
                atUtc = DateTime.UtcNow
            });

    public Task SeriesChangedAsync(string seriesId, string change)
        => _hub.Clients
            .Group(NotificationsHub.InstitutionGroup)
            .SendAsync("seriesChanged", new
            {
                seriesId,
                change,
                atUtc = DateTime.UtcNow
            });

    public Task ReminderDueAsync(ReminderContextDto context, bool silent)
        => _hub.Clients
            .Group(NotificationsHub.InstitutionGroup)
            .SendAsync("reminderDue", new
            {
                reminderId = context.ReminderId,
                appointmentId = context.AppointmentId,
                kind = context.Kind.ToString(),
                title = context.Kind == Domain.Enums.ReminderKind.Call
                    ? "Sıradaki kişiyi arama zamanı"
                    : "Sıradaki görüşme yaklaşıyor",
                body = context.NotificationBody,
                startsAtLocal = context.StartsAtLocal,
                timeLabel = context.TimeLabel,
                silent,
                atUtc = DateTime.UtcNow
            });
}
