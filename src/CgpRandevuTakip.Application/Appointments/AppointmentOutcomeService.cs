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

    public async Task<OperationResult> SaveAsync(
        string appointmentId,
        AppointmentOutcomeInput input)
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
            return OperationResult.Fail(
                $"Görüşme değerlendirmesi en fazla {MaxNoteLength} karakter olabilir.");

        var postNotes = NormalizeOptional(input.PostNotes);
        if (postNotes is { Length: > MaxNoteLength })
            return OperationResult.Fail(
                $"Görüşme sonrası açıklama en fazla {MaxNoteLength} karakter olabilir.");

        var now = _clock.UtcNow;
        var guests = appointment.Guests.ToList();

        if (guests.Count == 0 && appointment.Person is not null)
        {
            guests.Add(new AppointmentGuest
            {
                AppointmentId = appointment.Id,
                PersonId = appointment.PersonId,
                IsPrimary = true,
                SortOrder = 0
            });
        }

        if (input.GuestOutcomes is { Count: > 0 })
        {
            var error = ApplyGuestOutcomes(appointment, guests, input, now);
            if (error is not null)
                return OperationResult.Fail(error);
        }
        else
        {
            var primary = guests.FirstOrDefault(x => x.IsPrimary) ?? guests.FirstOrDefault();
            if (primary is not null)
            {
                primary.Attendance = input.Attendance;
                primary.Summary = summary;
                primary.PostNotes = postNotes;
                primary.WillRepeat = input.WillRepeat;
                primary.IsClosed = input.IsClosed;
                primary.ClosedAtUtc = input.IsClosed ? now : null;
                primary.ClosedByUserId = input.IsClosed ? _currentUser.UserId : null;
                primary.UpdatedAtUtc = now;
                primary.UpdatedByUserId = _currentUser.UserId;
            }

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
        }

        var aggregate = AggregateAttendance(guests, input.Attendance);

        if (input.GuestOutcomes is { Count: > 0 })
        {
            try
            {
                appointment.ApplyOutcome(
                    aggregate,
                    input.IsClosed,
                    now,
                    _currentUser.UserId);
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        var existing = appointment.Outcome ?? await _appointments.GetOutcomeAsync(appointmentId);
        var outcome = existing ?? new AppointmentOutcome
        {
            AppointmentId = appointmentId
        };

        outcome.Attendance = aggregate;
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

        await _appointments.SaveOutcomeAsync(
            appointment,
            outcome,
            guests,
            cancelReminders);

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
                outcome.WillRepeat,
                guests = guests.Select(x => new
                {
                    x.PersonId,
                    x.Attendance,
                    x.IsClosed
                }).ToList()
            }));

        return OperationResult.Ok();
    }

    private string? ApplyGuestOutcomes(
        Appointment appointment,
        List<AppointmentGuest> guests,
        AppointmentOutcomeInput input,
        DateTime now)
    {
        foreach (var guestInput in input.GuestOutcomes!)
        {
            var guest = guests.FirstOrDefault(x => x.Id == guestInput.GuestId);
            if (guest is null)
                return "Seçilen misafir bu görüşmede bulunamadı.";

            if (!Enum.IsDefined(guestInput.Attendance))
                return "Geçersiz misafir katılım durumu.";

            var guestSummary = NormalizeOptional(guestInput.Summary);
            if (guestSummary is { Length: > MaxNoteLength })
                return $"Misafir değerlendirmesi en fazla {MaxNoteLength} karakter olabilir.";

            var guestPostNotes = NormalizeOptional(guestInput.PostNotes);
            if (guestPostNotes is { Length: > MaxNoteLength })
                return $"Misafir sonrası açıklama en fazla {MaxNoteLength} karakter olabilir.";

            guest.Attendance = guestInput.Attendance;
            guest.Summary = guestSummary;
            guest.PostNotes = guestPostNotes;
            guest.WillRepeat = guestInput.WillRepeat;
            guest.IsClosed = guestInput.IsClosed;
            guest.ClosedAtUtc = guestInput.IsClosed ? now : null;
            guest.ClosedByUserId = guestInput.IsClosed ? _currentUser.UserId : null;
            guest.UpdatedAtUtc = now;
            guest.UpdatedByUserId = _currentUser.UserId;
        }

        if (input.IsClosed && guests.Any(x => x.Attendance is null))
            return "Görüşmeyi kapatmadan önce tüm misafirlerin katılım durumunu seçin.";

        return null;
    }

    private static Attendance AggregateAttendance(
        IReadOnlyList<AppointmentGuest> guests,
        Attendance fallback)
    {
        var values = guests
            .Where(x => x.Attendance is not null)
            .Select(x => x.Attendance!.Value)
            .ToList();

        if (values.Count == 0)
            return fallback;

        if (values.All(x => x == Attendance.NoShow))
            return Attendance.NoShow;

        if (values.All(x => x == Attendance.Cancelled))
            return Attendance.Cancelled;

        if (values.All(x => x == Attendance.Postponed))
            return Attendance.Postponed;

        if (values.Contains(Attendance.Arrived))
            return Attendance.Arrived;

        return values[0];
    }

    private bool CanAccess(Appointment appointment)
    {
        if (_currentUser.IsInRole(UserRole.Admin, UserRole.Secretary))
            return true;

        return _currentUser.IsInRole(UserRole.Host)
               && (appointment.HostUserId == _currentUser.UserId
                   || appointment.Participants.Any(x => x.UserId == _currentUser.UserId));
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

        var closedByNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var userId in appointment.Guests
                     .Where(x => !string.IsNullOrWhiteSpace(x.ClosedByUserId))
                     .Select(x => x.ClosedByUserId!)
                     .Distinct(StringComparer.Ordinal))
        {
            var user = await _users.GetByIdAsync(userId);
            if (user is not null)
                closedByNames[userId] = user.DisplayName;
        }

        var guests = appointment.Guests
            .OrderBy(x => x.SortOrder)
            .Select(x => ToGuestDto(x, closedByNames))
            .ToList();

        if (guests.Count == 0 && appointment.Person is not null)
        {
            guests.Add(new AppointmentGuestDto(
                appointment.Id,
                appointment.PersonId,
                appointment.Person.FirstName,
                appointment.Person.LastName,
                appointment.Person.PhoneE164,
                true,
                new AppointmentGuestOutcomeDto(
                    outcome.Attendance,
                    outcome.Summary,
                    outcome.WillRepeat,
                    outcome.PostNotes,
                    outcome.IsClosed,
                    outcome.ClosedAtUtc is null
                        ? null
                        : _clock.ToLocal(outcome.ClosedAtUtc.Value),
                    closedByName,
                    _clock.ToLocal(outcome.UpdatedAtUtc))));
        }

        return new AppointmentOutcomeDto(
            outcome.Attendance,
            outcome.Summary,
            outcome.WillRepeat,
            outcome.PostNotes,
            outcome.IsClosed,
            outcome.ClosedAtUtc is null ? null : _clock.ToLocal(outcome.ClosedAtUtc.Value),
            closedByName,
            _clock.ToLocal(outcome.UpdatedAtUtc),
            guests);
    }

    private AppointmentGuestDto ToGuestDto(
        AppointmentGuest guest,
        IReadOnlyDictionary<string, string> closedByNames)
    {
        AppointmentGuestOutcomeDto? outcome = null;

        if (guest.Attendance is { } attendance)
        {
            string? guestClosedByName = null;
            if (!string.IsNullOrWhiteSpace(guest.ClosedByUserId))
                closedByNames.TryGetValue(guest.ClosedByUserId, out guestClosedByName);

            outcome = new AppointmentGuestOutcomeDto(
                attendance,
                guest.Summary,
                guest.WillRepeat,
                guest.PostNotes,
                guest.IsClosed,
                guest.ClosedAtUtc is null
                    ? null
                    : _clock.ToLocal(guest.ClosedAtUtc.Value),
                guestClosedByName,
                _clock.ToLocal(guest.UpdatedAtUtc));
        }

        return new AppointmentGuestDto(
            guest.Id,
            guest.PersonId,
            guest.Person?.FirstName ?? string.Empty,
            guest.Person?.LastName ?? string.Empty,
            guest.Person?.PhoneE164 ?? string.Empty,
            guest.IsPrimary,
            outcome);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
