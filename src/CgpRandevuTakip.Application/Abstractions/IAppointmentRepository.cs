using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

/// <summary>Görüşme sorgusu. Tarihler UTC'dir; katman dönüşümü uygulama servisinde yapılır.</summary>
public sealed record AppointmentQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? SearchText,
    string? CategoryId,
    string? HostUserId,
    AppointmentStatus? Status);

public sealed record AppointmentConflictRecord(
    string AppointmentId,
    string PersonFirstName,
    string PersonLastName,
    string PhoneE164,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? CategoryName);

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(string id);
    Task<AppointmentOutcome?> GetOutcomeAsync(string appointmentId);
    Task<IReadOnlyList<Appointment>> QueryAsync(AppointmentQuery query);
    Task<Appointment?> GetNextAsync(DateTime nowUtc, string? hostUserId);
    Task<IReadOnlyList<AppointmentConflictRecord>> FindHostConflictsAsync(
        string hostUserId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId);
    Task<IReadOnlyList<ReminderDispatch>> GetRemindersAsync(string appointmentId);
    Task<IReadOnlyList<ReminderDispatch>> GetDueRemindersAsync(DateTime nowUtc, int batchSize);
    Task<IReadOnlyList<ReminderDispatch>> GetExpiredSentRemindersAsync(DateTime cutoffUtc);
    Task<ReminderDispatch?> GetReminderAsync(string id);
    Task MarkWindowsNotifiedAsync(string id, DateTime nowUtc);
    Task MarkPushNotifiedAsync(string id, DateTime nowUtc);
    Task MarkSentAsync(string id);
    Task MarkMissedAsync(string id, DateTime nowUtc);
    Task RequeueForRepeatAsync(string id, DateTime nowUtc);
    Task RequeueForSnoozeAsync(string id, DateTime dueAtUtc, DateTime nowUtc, string? userId);
    Task MarkActionAsync(
        string id,
        ReminderState state,
        ReminderAction action,
        DateTime nowUtc,
        string? userId);
    Task MarkUnansweredAsync(IReadOnlyList<string> ids);
    Task<IReadOnlyList<Appointment>> GetDeletedAsync();
    Task<IReadOnlyDictionary<string, int>> GetHostUsageCountsAsync();
    Task AddAsync(Appointment appointment);
    Task SaveAsync(
        Appointment appointment,
        IReadOnlyList<ReminderDispatch>? newReminders,
        bool cancelPendingReminders);
    Task SaveOutcomeAsync(
        Appointment appointment,
        AppointmentOutcome outcome,
        bool cancelPendingReminders);
    Task SoftDeleteAsync(string id, DateTime nowUtc);
    Task<bool> RestoreAsync(string id, DateTime nowUtc);
    Task<IReadOnlyList<string>> PurgeDeletedBeforeAsync(DateTime cutoffUtc);
}
