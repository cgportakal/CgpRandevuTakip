using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Reminders;

public sealed class ReminderService : IReminderService
{
    private static readonly TimeSpan SnoozeDelay = TimeSpan.FromMinutes(10);

    private readonly IAppointmentRepository _repository;
    private readonly IAppointmentOutcomeService _outcome;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public ReminderService(
        IAppointmentRepository repository,
        IAppointmentOutcomeService outcome,
        ICurrentUser currentUser,
        IClock clock,
        IAuditLogger audit)
    {
        _repository = repository;
        _outcome = outcome;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ReminderContextDto>> GetDueAsync(DateTime nowUtc, int batchSize)
    {
        var rows = await _repository.GetDueRemindersAsync(nowUtc, batchSize);
        return rows.Select(ToContext).ToList();
    }

    public async Task<int> RequeueExpiredAsync(DateTime nowUtc, TimeSpan repeatDelay, int maxRepeats)
    {
        var rows = await _repository.GetExpiredSentRemindersAsync(nowUtc - repeatDelay);

        var toRepeat = rows.Where(x => x.RepeatCount < maxRepeats).ToList();
        var toUnanswered = rows
            .Where(x => x.RepeatCount >= maxRepeats)
            .Select(x => x.Id)
            .ToList();

        foreach (var reminder in toRepeat)
            await _repository.RequeueForRepeatAsync(reminder.Id, nowUtc);

        await _repository.MarkUnansweredAsync(toUnanswered);

        return toRepeat.Count;
    }

    public Task MarkWindowsNotifiedAsync(string reminderId, DateTime nowUtc)
        => _repository.MarkWindowsNotifiedAsync(reminderId, nowUtc);

    public Task MarkPushNotifiedAsync(string reminderId, DateTime nowUtc)
        => _repository.MarkPushNotifiedAsync(reminderId, nowUtc);

    public Task MarkSentAsync(string reminderId)
        => _repository.MarkSentAsync(reminderId);

    public Task MarkMissedAsync(string reminderId, DateTime nowUtc)
        => _repository.MarkMissedAsync(reminderId, nowUtc);

    public async Task<OperationResult> ApplyActionAsync(
        string reminderId,
        ReminderAction action,
        DateTime nowUtc)
    {
        var reminder = await _repository.GetReminderAsync(reminderId);
        if (reminder is null)
            return OperationResult.Fail("Hatırlatıcı bulunamadı.");

        var userId = _currentUser.UserId;

        switch (action)
        {
            case ReminderAction.DismissOnly:
                await _repository.MarkActionAsync(
                    reminderId, ReminderState.DismissedOnly, ReminderAction.DismissOnly, nowUtc, userId);
                break;

            case ReminderAction.InformedHost:
                await _repository.MarkActionAsync(
                    reminderId, ReminderState.InformedHost, ReminderAction.InformedHost, nowUtc, userId);
                break;

            case ReminderAction.UnreachableSnooze:
                await _repository.RequeueForSnoozeAsync(
                    reminderId, nowUtc + SnoozeDelay, nowUtc, userId);
                break;

            case ReminderAction.VisitorCancelled:
                if (string.IsNullOrWhiteSpace(reminder.AppointmentId))
                    return OperationResult.Fail("Görüşme bilgisi bulunamadı.");

                var outcome = await _outcome.SaveAsync(
                    reminder.AppointmentId,
                    new AppointmentOutcomeInput(
                        Attendance.Cancelled,
                        "Ziyaretçi görüşmeyi iptal etti.",
                        WillRepeat: false,
                        PostNotes: null,
                        IsClosed: true));

                if (!outcome.Success)
                    return outcome;

                await _repository.MarkActionAsync(
                    reminderId, ReminderState.Cancelled, ReminderAction.VisitorCancelled, nowUtc, userId);
                break;

            default:
                return OperationResult.Fail("Bilinmeyen hatırlatıcı aksiyonu.");
        }

        await _audit.LogAsync(
            AuditAction.ReminderActionTaken,
            userId,
            nameof(ReminderDispatch),
            reminderId);

        return OperationResult.Ok();
    }

    private ReminderContextDto ToContext(ReminderDispatch reminder)
    {
        var appointment = reminder.Appointment;
        var person = appointment?.Person;

        return new ReminderContextDto(
            reminder.Id,
            reminder.AppointmentId,
            reminder.Kind,
            reminder.OffsetMinutes,
            _clock.ToLocal(appointment?.StartsAtUtc ?? reminder.DueAtUtc),
            _clock.ToLocal(reminder.DueAtUtc),
            $"{person?.FirstName} {person?.LastName}".Trim(),
            appointment?.HostUser?.DisplayName ?? string.Empty,
            appointment?.Category?.Name ?? string.Empty,
            person?.PhoneE164 ?? string.Empty,
            appointment?.Method ?? AppointmentMethod.FaceToFace,
            appointment?.Status ?? AppointmentStatus.Scheduled,
            reminder.WindowsNotifiedAtUtc != null,
            reminder.PushNotifiedAtUtc != null,
            appointment is not null && appointment.IsOverdue(_clock.UtcNow));
    }
}
