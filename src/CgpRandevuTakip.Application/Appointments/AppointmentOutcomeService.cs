using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Appointments;

public sealed class AppointmentOutcomeService : IAppointmentOutcomeService
{
    private const int MaxNoteLength = 4000;

    private readonly IAppointmentRepository _appointments;
    private readonly IUserRepository _users;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AppointmentOutcomeService(
        IAppointmentRepository appointments,
        IUserRepository users,
        IAuditLogger audit,
        IClock clock,
        ICurrentUser currentUser)
    {
        _appointments = appointments;
        _users = users;
        _audit = audit;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<AppointmentOutcomeDto?> GetAsync(string appointmentId)
    {
        var appointment = await _appointments.GetByIdAsync(appointmentId);
        if (appointment is null || appointment.IsDeleted || !CanAccess(appointment))
            return null;

        var outcome = await _appointments.GetOutcomeAsync(appointmentId);
        return outcome is null ? null : await ToDtoAsync(outcome, appointment);
    }

    public async Task<OperationResult> SaveAsync(string appointmentId, AppointmentOutcomeInput input)
    {
        if (_currentUser.Role is null)
            return OperationResult.Fail("Oturum bulunamadı. Lütfen yeniden giriş yapın.");

        if (!Enum.IsDefined(input.Attendance))
            return OperationResult.Fail("Geçersiz katılım durumu.");

        var appointment = await _appointments.GetByIdAsync(appointmentId);
        if (appointment is null || appointment.IsDeleted)
            return OperationResult.Fail("Görüşme bulunamadı.");

        if (!CanAccess(appointment))
            return OperationResult.Fail("Bu görüşmenin sonucunu girme yetkiniz yok.");

        var summary = NormalizeOptional(input.Summary);
        if (summary is { Length: > MaxNoteLength })
            return OperationResult.Fail($"Görüşme değerlendirmesi en fazla {MaxNoteLength} karakter olabilir.");

        var postNotes = NormalizeOptional(input.PostNotes);
        if (postNotes is { Length: > MaxNoteLength })
            return OperationResult.Fail($"Görüşme sonrası açıklama en fazla {MaxNoteLength} karakter olabilir.");

        var now = _clock.UtcNow;
        try
        {
            appointment.ApplyOutcome(
                input.Attendance,
                input.IsClosed,
                now,
                _currentUser.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult.Fail(ex.Message);
        }

        var existing = appointment.Outcome ?? await _appointments.GetOutcomeAsync(appointmentId);
        var outcome = existing ?? new AppointmentOutcome
        {
            AppointmentId = appointmentId
        };

        outcome.Attendance = input.Attendance;
        outcome.Summary = summary;
        outcome.WillRepeat = input.WillRepeat;
        outcome.PostNotes = postNotes;
        outcome.IsClosed = appointment.IsClosed;
        outcome.ClosedAtUtc = appointment.ClosedAtUtc;
        outcome.UpdatedAtUtc = now;
        outcome.UpdatedByUserId = _currentUser.UserId;

        var cancelReminders = appointment.Status is
            AppointmentStatus.Cancelled
            or AppointmentStatus.Completed
            or AppointmentStatus.NoShow
            or AppointmentStatus.Postponed;

        await _appointments.SaveOutcomeAsync(appointment, outcome, cancelReminders);

        await _audit.LogAsync(
            AuditAction.OutcomeSaved,
            _currentUser.UserId,
            nameof(Appointment),
            appointment.Id,
            JsonSerializer.Serialize(new
            {
                appointment.Status,
                outcome.Attendance,
                outcome.IsClosed,
                outcome.WillRepeat
            }));

        return OperationResult.Ok();
    }

    private bool CanAccess(Appointment appointment)
    {
        if (_currentUser.IsInRole(UserRole.Admin, UserRole.Secretary))
            return true;

        return _currentUser.IsInRole(UserRole.Host)
               && appointment.HostUserId == _currentUser.UserId;
    }

    private async Task<AppointmentOutcomeDto> ToDtoAsync(
        AppointmentOutcome outcome,
        Appointment appointment)
    {
        string? closedByName = null;
        if (appointment.ClosedByUserId is { Length: > 0 } closedById)
        {
            var user = await _users.GetByIdAsync(closedById);
            closedByName = user?.DisplayName;
        }

        return new AppointmentOutcomeDto(
            outcome.Attendance,
            outcome.Summary,
            outcome.WillRepeat,
            outcome.PostNotes,
            outcome.IsClosed,
            outcome.ClosedAtUtc is null ? null : _clock.ToLocal(outcome.ClosedAtUtc.Value),
            closedByName,
            _clock.ToLocal(outcome.UpdatedAtUtc));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
