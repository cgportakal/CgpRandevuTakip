using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Domain.ValueObjects;

namespace CgpRandevuTakip.Application.Appointments;

public sealed class AppointmentService : IAppointmentService
{
    private const int MinDurationMinutes = 5;
    private const int MaxDurationMinutes = 240;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 4000;
    private const int DeletedRetentionDays = 30;

    private static readonly ReminderState[] AlreadyNotifiedStates =
    {
        ReminderState.Sent,
        ReminderState.DismissedOnly,
        ReminderState.InformedHost,
        ReminderState.Snoozed,
        ReminderState.Missed,
        ReminderState.Unanswered
    };

    private readonly IAppointmentRepository _appointments;
    private readonly IPersonRepository _persons;
    private readonly IUserRepository _users;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AppointmentService(
        IAppointmentRepository appointments,
        IPersonRepository persons,
        IUserRepository users,
        ICategoryRepository categories,
        IAuditLogger audit,
        IClock clock,
        ICurrentUser currentUser)
    {
        _appointments = appointments;
        _persons = persons;
        _users = users;
        _categories = categories;
        _audit = audit;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> GetDayAsync(DateTime localDate)
    {
        var fromUtc = _clock.ToUtc(localDate.Date);
        var toUtc = _clock.ToUtc(localDate.Date.AddDays(1));

        var rows = await _appointments.QueryAsync(new AppointmentQuery(
            fromUtc,
            toUtc,
            SearchText: null,
            CategoryId: null,
            HostUserId: ScopedHostUserId(),
            Status: null));

        return rows.Select(ToListItem).ToList();
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(
        string? searchText,
        string? categoryId,
        AppointmentStatus? status,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId = null)
    {
        var term = searchText?.Trim();
        var hasTerm = !string.IsNullOrWhiteSpace(term);

        DateTime? fromUtc = fromLocal is null ? null : _clock.ToUtc(fromLocal.Value.Date);
        DateTime? toUtc = toLocal is null ? null : _clock.ToUtc(toLocal.Value.Date.AddDays(1));

        if (!hasTerm && fromUtc is null && toUtc is null)
        {
            fromUtc = _clock.ToUtc(DateTime.Today);
            toUtc = _clock.ToUtc(DateTime.Today.AddDays(1));
        }

        // Host rolü kendi kayıtlarıyla sınırlıdır; yönetici/sekreter filtre uygulayabilir.
        var hostFilter = ScopedHostUserId() ?? NormalizeOptional(hostUserId);

        var rows = await _appointments.QueryAsync(new AppointmentQuery(
            fromUtc,
            toUtc,
            hasTerm ? term : null,
            string.IsNullOrWhiteSpace(categoryId) ? null : categoryId,
            hostFilter,
            status));

        return rows.Select(ToListItem).ToList();
    }

    public async Task<AppointmentDetailDto?> GetDetailAsync(string id)
    {
        var appointment = await _appointments.GetByIdAsync(id);
        if (appointment is null)
            return null;

        if (_currentUser.IsInRole(UserRole.Host) && appointment.HostUserId != _currentUser.UserId)
            return null;

        var reminders = await _appointments.GetRemindersAsync(id);
        var closedByName = await ResolveClosedByNameAsync(appointment.ClosedByUserId);
        return ToDetail(appointment, reminders, closedByName);
    }

    public async Task<AppointmentListItemDto?> GetNextAppointmentAsync()
    {
        var next = await _appointments.GetNextAsync(_clock.UtcNow, ScopedHostUserId());
        return next is null ? null : ToListItem(next);
    }

    public async Task<IReadOnlyList<DeletedAppointmentDto>> GetDeletedAsync()
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return Array.Empty<DeletedAppointmentDto>();

        var rows = await _appointments.GetDeletedAsync();
        return rows.Select(ToDeletedDto).ToList();
    }

    public async Task<AppointmentSaveResult> CreateAsync(AppointmentInput input)
    {
        if (!CanManageAppointments())
            return AppointmentSaveResult.Fail("Görüşme ekleme yetkiniz yok.");

        var validation = await ValidateAsync(input, currentAppointmentId: null);
        if (validation.Error is not null)
            return AppointmentSaveResult.Fail(validation.Error);

        var startsAtUtc = _clock.ToUtc(input.StartsAtLocal);
        var endsAtUtc = startsAtUtc.AddMinutes(input.DurationMinutes);

        if (!input.IgnoreConflicts)
        {
            var conflicts = await FindConflictsAsync(
                validation.PhoneE164!,
                validation.HostUserId!,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: null);

            if (conflicts.Count > 0)
                return AppointmentSaveResult.Conflict(BuildConflictMessage(conflicts), conflicts);
        }

        var person = await ResolvePersonAsync(
            validation.FirstName!,
            validation.LastName!,
            validation.PhoneE164!);

        var now = _clock.UtcNow;
        var appointment = new Appointment
        {
            PersonId = person.Id,
            HostUserId = validation.HostUserId!,
            CategoryId = validation.CategoryId!,
            StartsAtUtc = startsAtUtc,
            DurationMinutes = input.DurationMinutes,
            Method = input.Method,
            Status = AppointmentStatus.Scheduled,
            Description = NormalizeOptional(input.Description),
            StudentFirstName = NormalizeOptional(input.StudentFirstName),
            StudentLastName = NormalizeOptional(input.StudentLastName),
            UpdatedAtUtc = now,
            CreatedByUserId = _currentUser.UserId,
            UpdatedByUserId = _currentUser.UserId,
            RowVersion = 1
        };

        foreach (var plan in ReminderScheduleCalculator.Calculate(
                     startsAtUtc,
                     input.ReminderOffsetsMinutes,
                     now))
        {
            appointment.Reminders.Add(new ReminderDispatch
            {
                AppointmentId = appointment.Id,
                Kind = ReminderKind.Appointment,
                OffsetMinutes = plan.OffsetMinutes,
                DueAtUtc = plan.DueAtUtc,
                State = ReminderState.Pending,
                DedupKey = ReminderScheduleCalculator.BuildDedupKey(
                    appointment.Id,
                    ReminderKind.Appointment,
                    plan.OffsetMinutes,
                    appointment.RowVersion)
            });
        }

        // "Sırası gelen kişi" arama hatırlatıcıları: 30 dk ve 5 dk önce.
        // 30 dk zaten randevu hatırlatıcısı olarak planlandıysa mükerrer bildirim önlenir.
        foreach (var plan in ReminderScheduleCalculator.CalculateCallReminders(startsAtUtc, now))
        {
            if (plan.OffsetMinutes == 30 && input.ReminderOffsetsMinutes.Contains(30))
                continue;

            appointment.Reminders.Add(new ReminderDispatch
            {
                AppointmentId = appointment.Id,
                Kind = ReminderKind.Call,
                OffsetMinutes = plan.OffsetMinutes,
                DueAtUtc = plan.DueAtUtc,
                State = ReminderState.Pending,
                DedupKey = ReminderScheduleCalculator.BuildDedupKey(
                    appointment.Id,
                    ReminderKind.Call,
                    plan.OffsetMinutes,
                    appointment.RowVersion)
            });
        }

        await _appointments.AddAsync(appointment);

        await _audit.LogAsync(
            AuditAction.AppointmentCreated,
            _currentUser.UserId,
            nameof(Appointment),
            appointment.Id,
            BuildAuditDetails(appointment));

        return AppointmentSaveResult.Ok(appointment.Id);
    }

    public async Task<AppointmentSaveResult> UpdateAsync(string id, AppointmentInput input)
    {
        if (!CanManageAppointments())
            return AppointmentSaveResult.Fail("Görüşme düzenleme yetkiniz yok.");

        var appointment = await _appointments.GetByIdAsync(id);
        if (appointment is null || appointment.IsDeleted)
            return AppointmentSaveResult.Fail("Görüşme bulunamadı.");

        var validation = await ValidateAsync(input, currentAppointmentId: id);
        if (validation.Error is not null)
            return AppointmentSaveResult.Fail(validation.Error);

        var startsAtUtc = _clock.ToUtc(input.StartsAtLocal);
        var endsAtUtc = startsAtUtc.AddMinutes(input.DurationMinutes);

        if (!input.IgnoreConflicts)
        {
            var conflicts = await FindConflictsAsync(
                validation.PhoneE164!,
                validation.HostUserId!,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: id);

            if (conflicts.Count > 0)
                return AppointmentSaveResult.Conflict(BuildConflictMessage(conflicts), conflicts);
        }

        var person = await ResolvePersonAsync(
            validation.FirstName!,
            validation.LastName!,
            validation.PhoneE164!);

        var now = _clock.UtcNow;
        var existingReminders = await _appointments.GetRemindersAsync(id);
        var blockedAppointmentOffsets = existingReminders
            .Where(x => x.Kind == ReminderKind.Appointment
                        && AlreadyNotifiedStates.Contains(x.State))
            .Select(x => x.OffsetMinutes)
            .ToHashSet();

        var blockedCallOffsets = existingReminders
            .Where(x => x.Kind == ReminderKind.Call
                        && AlreadyNotifiedStates.Contains(x.State))
            .Select(x => x.OffsetMinutes)
            .ToHashSet();

        appointment.PersonId = person.Id;
        appointment.HostUserId = validation.HostUserId!;
        appointment.CategoryId = validation.CategoryId!;
        appointment.StartsAtUtc = startsAtUtc;
        appointment.DurationMinutes = input.DurationMinutes;
        appointment.Method = input.Method;
        appointment.Description = NormalizeOptional(input.Description);
        appointment.StudentFirstName = NormalizeOptional(input.StudentFirstName);
        appointment.StudentLastName = NormalizeOptional(input.StudentLastName);
        appointment.UpdatedAtUtc = now;
        appointment.UpdatedByUserId = _currentUser.UserId;
        appointment.RowVersion++;

        var newAppointmentReminders = ReminderScheduleCalculator
            .Calculate(startsAtUtc, input.ReminderOffsetsMinutes, now)
            .Where(plan => !blockedAppointmentOffsets.Contains(plan.OffsetMinutes))
            .Select(plan => new ReminderDispatch
            {
                AppointmentId = appointment.Id,
                Kind = ReminderKind.Appointment,
                OffsetMinutes = plan.OffsetMinutes,
                DueAtUtc = plan.DueAtUtc,
                State = ReminderState.Pending,
                DedupKey = ReminderScheduleCalculator.BuildDedupKey(
                    appointment.Id,
                    ReminderKind.Appointment,
                    plan.OffsetMinutes,
                    appointment.RowVersion)
            });

        var newCallReminders = ReminderScheduleCalculator
            .CalculateCallReminders(startsAtUtc, now)
            .Where(plan => !blockedCallOffsets.Contains(plan.OffsetMinutes))
            .Where(plan => plan.OffsetMinutes != 30 || !input.ReminderOffsetsMinutes.Contains(30))
            .Select(plan => new ReminderDispatch
            {
                AppointmentId = appointment.Id,
                Kind = ReminderKind.Call,
                OffsetMinutes = plan.OffsetMinutes,
                DueAtUtc = plan.DueAtUtc,
                State = ReminderState.Pending,
                DedupKey = ReminderScheduleCalculator.BuildDedupKey(
                    appointment.Id,
                    ReminderKind.Call,
                    plan.OffsetMinutes,
                    appointment.RowVersion)
            });

        var newReminders = newAppointmentReminders.Concat(newCallReminders).ToList();

        await _appointments.SaveAsync(
            appointment,
            newReminders,
            cancelPendingReminders: true);

        await _audit.LogAsync(
            AuditAction.AppointmentUpdated,
            _currentUser.UserId,
            nameof(Appointment),
            appointment.Id,
            BuildAuditDetails(appointment));

        return AppointmentSaveResult.Ok(appointment.Id);
    }

    public async Task<OperationResult> DeleteAsync(string id)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return OperationResult.Fail("Görüşme silme yetkisi yalnızca yöneticidedir.");

        var appointment = await _appointments.GetByIdAsync(id);
        if (appointment is null || appointment.IsDeleted)
            return OperationResult.Fail("Görüşme bulunamadı.");

        await _appointments.SoftDeleteAsync(id, _clock.UtcNow);
        await _audit.LogAsync(
            AuditAction.AppointmentDeleted,
            _currentUser.UserId,
            nameof(Appointment),
            id);

        return OperationResult.Ok();
    }

    public async Task<BulkDeleteResult> DeleteManyAsync(IReadOnlyList<string> ids)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return new BulkDeleteResult(0, ids?.ToList() ?? new List<string>(),
                "Görüşme silme yetkisi yalnızca yöneticidedir.");

        var distinctIds = ids?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList() ?? new List<string>();

        if (distinctIds.Count == 0)
            return new BulkDeleteResult(0, Array.Empty<string>(), "Silinecek görüşme seçilmedi.");

        var deleted = 0;
        var failed = new List<string>();
        foreach (var id in distinctIds)
        {
            var result = await DeleteAsync(id);
            if (result.Success)
                deleted++;
            else
                failed.Add(id);
        }

        return new BulkDeleteResult(deleted, failed);
    }

    public async Task<BulkMoveResult> MoveManyAsync(
        IReadOnlyList<string> ids,
        DateTime newDate,
        TimeSpan? newTime,
        bool ignoreConflicts = false)
    {
        if (!CanManageAppointments())
            return new BulkMoveResult(
                0,
                Array.Empty<string>(),
                Array.Empty<AppointmentConflictDto>(),
                ids?.ToList() ?? new List<string>(),
                "Görüşme taşıma yetkiniz yok.");

        var distinctIds = ids?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList() ?? new List<string>();

        if (distinctIds.Count == 0)
            return new BulkMoveResult(
                0,
                Array.Empty<string>(),
                Array.Empty<AppointmentConflictDto>(),
                Array.Empty<string>(),
                "Taşınacak görüşme seçilmedi.");

        var moved = 0;
        var conflictingIds = new List<string>();
        var conflicts = new List<AppointmentConflictDto>();
        var failed = new List<string>();

        foreach (var id in distinctIds)
        {
            var detail = await GetDetailAsync(id);
            if (detail is null)
            {
                failed.Add(id);
                continue;
            }

            // Saat korunuyorsa her görüşme kendi saatinde yeni güne taşınır.
            var startLocal = newDate.Date + (newTime ?? detail.StartsAtLocal.TimeOfDay);
            var input = new AppointmentInput(
                detail.PersonFirstName,
                detail.PersonLastName,
                detail.PhoneE164,
                startLocal,
                detail.DurationMinutes,
                detail.HostUserId,
                detail.CategoryId,
                detail.Method,
                detail.Description,
                detail.StudentFirstName,
                detail.StudentLastName,
                detail.ReminderOffsetsMinutes,
                ignoreConflicts);

            var result = await UpdateAsync(id, input);
            if (result.Success)
            {
                moved++;
            }
            else if (result.Conflicts is { Count: > 0 })
            {
                conflictingIds.Add(id);
                conflicts.AddRange(result.Conflicts);
            }
            else
            {
                failed.Add(id);
            }
        }

        return new BulkMoveResult(moved, conflictingIds, conflicts, failed);
    }

    public async Task<OperationResult> RestoreAsync(string id)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return OperationResult.Fail("Geri alma yetkisi yalnızca yöneticidedir.");

        var appointment = await _appointments.GetByIdAsync(id);
        if (appointment is null || !appointment.IsDeleted)
            return OperationResult.Fail("Görüşme bulunamadı veya zaten aktif.");

        var now = _clock.UtcNow;
        if (appointment.DeletedAtUtc is { } deletedAt
            && deletedAt < now.AddDays(-DeletedRetentionDays))
        {
            return OperationResult.Fail(
                $"Geri alma süresi doldu. Silinen görüşmeler {DeletedRetentionDays} gün saklanır.");
        }

        var restored = await _appointments.RestoreAsync(id, now);
        if (!restored)
            return OperationResult.Fail("Görüşme geri alınamadı.");

        await _audit.LogAsync(
            AuditAction.AppointmentRestored,
            _currentUser.UserId,
            nameof(Appointment),
            id);

        return OperationResult.Ok();
    }

    public async Task<int> PurgeExpiredDeletedAsync(DateTime nowUtc)
    {
        var ids = await _appointments.PurgeDeletedBeforeAsync(
            nowUtc.AddDays(-DeletedRetentionDays));

        foreach (var id in ids)
        {
            await _audit.LogAsync(
                AuditAction.AppointmentPurged,
                userId: null,
                entityType: nameof(Appointment),
                entityId: id);
        }

        return ids.Count;
    }

    private bool CanManageAppointments()
        => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    private string? ScopedHostUserId()
        => _currentUser.IsInRole(UserRole.Host) ? _currentUser.UserId : null;

    private async Task<ValidationResult> ValidateAsync(AppointmentInput input, string? currentAppointmentId)
    {
        var firstName = input.FirstName?.Trim() ?? string.Empty;
        var lastName = input.LastName?.Trim() ?? string.Empty;

        if (firstName.Length == 0)
            return ValidationResult.Fail("Ad zorunludur.");

        if (firstName.Length > MaxNameLength)
            return ValidationResult.Fail($"Ad en fazla {MaxNameLength} karakter olabilir.");

        if (lastName.Length == 0)
            return ValidationResult.Fail("Soyad zorunludur.");

        if (lastName.Length > MaxNameLength)
            return ValidationResult.Fail($"Soyad en fazla {MaxNameLength} karakter olabilir.");

        if (!PhoneNumber.TryNormalize(input.Phone, out var phoneE164))
            return ValidationResult.Fail("Telefon numarası geçerli değil. Örnek: 0532 123 45 67.");

        if (input.StartsAtLocal == default)
            return ValidationResult.Fail("Görüşme tarihi ve saati zorunludur.");

        if (input.DurationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
            return ValidationResult.Fail(
                $"Görüşme süresi {MinDurationMinutes}–{MaxDurationMinutes} dakika arasında olmalıdır.");

        if (string.IsNullOrWhiteSpace(input.HostUserId))
            return ValidationResult.Fail("Görüşülecek kişi (host) seçilmelidir.");

        var host = await _users.GetByIdAsync(input.HostUserId);
        if (host is null || !host.IsActive)
            return ValidationResult.Fail("Seçilen host bulunamadı veya pasif.");

        if (string.IsNullOrWhiteSpace(input.CategoryId))
            return ValidationResult.Fail("Kategori seçilmelidir.");

        var category = await _categories.GetByIdAsync(input.CategoryId);
        if (category is null)
            return ValidationResult.Fail("Seçilen kategori bulunamadı.");

        if (!category.IsActive)
        {
            var current = currentAppointmentId is null
                ? null
                : await _appointments.GetByIdAsync(currentAppointmentId);

            if (current is null || current.CategoryId != category.Id)
                return ValidationResult.Fail("Pasif kategori yeni görüşmelerde seçilemez.");
        }

        if (!Enum.IsDefined(input.Method))
            return ValidationResult.Fail("Geçersiz görüşme yöntemi.");

        var description = input.Description?.Trim();
        if (description is { Length: > MaxDescriptionLength })
            return ValidationResult.Fail(
                $"Açıklama en fazla {MaxDescriptionLength} karakter olabilir.");

        if (input.StudentFirstName?.Trim().Length > MaxNameLength)
            return ValidationResult.Fail($"Öğrenci adı en fazla {MaxNameLength} karakter olabilir.");

        if (input.StudentLastName?.Trim().Length > MaxNameLength)
            return ValidationResult.Fail($"Öğrenci soyadı en fazla {MaxNameLength} karakter olabilir.");

        var offsets = input.ReminderOffsetsMinutes ?? Array.Empty<int>();
        var invalidOffset = offsets.FirstOrDefault(x => !ReminderScheduleCalculator.IsAllowedOffset(x));
        if (invalidOffset != 0)
            return ValidationResult.Fail(
                $"Hatırlatıcı aralığı 1–{ReminderScheduleCalculator.MaxOffsetMinutes} dakika arasında olmalıdır.");

        return ValidationResult.Ok(firstName, lastName, phoneE164, input.HostUserId, input.CategoryId);
    }

    private async Task<IReadOnlyList<AppointmentConflictDto>> FindConflictsAsync(
        string phoneE164,
        string hostUserId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId)
    {
        var records = await _appointments.FindHostConflictsAsync(
            hostUserId,
            startsAtUtc,
            endsAtUtc,
            excludeAppointmentId);

        return records.Select(x => new AppointmentConflictDto(
                x.AppointmentId,
                $"{x.PersonFirstName} {x.PersonLastName}".Trim(),
                _clock.ToLocal(x.StartsAtUtc),
                _clock.ToLocal(x.EndsAtUtc),
                x.CategoryName,
                string.Equals(x.PhoneE164, phoneE164, StringComparison.Ordinal)))
            .ToList();
    }

    private static string BuildConflictMessage(IReadOnlyList<AppointmentConflictDto> conflicts)
    {
        var first = conflicts[0];
        var samePerson = conflicts.Any(x => x.IsSamePerson);
        var prefix = samePerson
            ? "Aynı kişi ve host için çakışan bir görüşme var"
            : "Host'un bu saatte başka bir görüşmesi var";

        return $"{prefix}: {first.StartsAtLocal:dd/MM/yyyy HH:mm} · {first.PersonName}. Yine de kaydedilsin mi?";
    }

    private async Task<Person> ResolvePersonAsync(string firstName, string lastName, string phoneE164)
    {
        var existing = await _persons.FindByPhoneAndNameAsync(phoneE164, firstName, lastName);
        if (existing is not null)
            return existing;

        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneE164 = phoneE164
        };

        await _persons.AddAsync(person);
        return person;
    }

    private DeletedAppointmentDto ToDeletedDto(Appointment appointment)
    {
        var startsAtLocal = _clock.ToLocal(appointment.StartsAtUtc);
        var personName = $"{appointment.Person?.FirstName} {appointment.Person?.LastName}".Trim();

        return new DeletedAppointmentDto(
            appointment.Id,
            startsAtLocal,
            personName,
            appointment.Person?.PhoneE164 ?? string.Empty,
            appointment.HostUser?.DisplayName ?? string.Empty,
            appointment.Category?.Name ?? string.Empty,
            _clock.ToLocal(appointment.DeletedAtUtc ?? appointment.UpdatedAtUtc));
    }

    private AppointmentListItemDto ToListItem(Appointment appointment)
    {
        var startsAtLocal = _clock.ToLocal(appointment.StartsAtUtc);

        return new AppointmentListItemDto(
            appointment.Id,
            startsAtLocal,
            startsAtLocal.AddMinutes(appointment.DurationMinutes),
            appointment.DurationMinutes,
            appointment.Person?.FirstName ?? string.Empty,
            appointment.Person?.LastName ?? string.Empty,
            appointment.Person?.PhoneE164 ?? string.Empty,
            appointment.HostUserId,
            appointment.HostUser?.DisplayName ?? string.Empty,
            appointment.CategoryId,
            appointment.Category?.Name ?? string.Empty,
            appointment.Category?.ColorHex ?? "#5D7166",
            appointment.Method,
            appointment.Status,
            appointment.IsOverdue(_clock.UtcNow),
            appointment.IsDeleted,
            NullIfEmpty(appointment.StudentFullName));
    }

    private AppointmentDetailDto ToDetail(
        Appointment appointment,
        IReadOnlyList<ReminderDispatch> reminders,
        string? closedByName)
    {
        var startsAtLocal = _clock.ToLocal(appointment.StartsAtUtc);

        var outcome = appointment.Outcome is null
            ? null
            : new AppointmentOutcomeDto(
                appointment.Outcome.Attendance,
                appointment.Outcome.Summary,
                appointment.Outcome.WillRepeat,
                appointment.Outcome.PostNotes,
                appointment.Outcome.IsClosed,
                appointment.Outcome.ClosedAtUtc is null
                    ? null
                    : _clock.ToLocal(appointment.Outcome.ClosedAtUtc.Value),
                closedByName,
                _clock.ToLocal(appointment.Outcome.UpdatedAtUtc));

        return new AppointmentDetailDto(
            appointment.Id,
            startsAtLocal,
            startsAtLocal.AddMinutes(appointment.DurationMinutes),
            appointment.DurationMinutes,
            appointment.Person?.FirstName ?? string.Empty,
            appointment.Person?.LastName ?? string.Empty,
            appointment.Person?.PhoneE164 ?? string.Empty,
            appointment.HostUserId,
            appointment.HostUser?.DisplayName ?? string.Empty,
            appointment.CategoryId,
            appointment.Category?.Name ?? string.Empty,
            appointment.Category?.ColorHex ?? "#5D7166",
            appointment.Method,
            appointment.Status,
            appointment.Description,
            appointment.StudentFirstName,
            appointment.StudentLastName,
            reminders
                .Where(x => x.Kind == ReminderKind.Appointment && x.State == ReminderState.Pending)
                .Select(x => x.OffsetMinutes)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList(),
            appointment.IsOverdue(_clock.UtcNow),
            appointment.IsClosed,
            appointment.IsDeleted,
            _clock.ToLocal(appointment.CreatedAtUtc),
            _clock.ToLocal(appointment.UpdatedAtUtc),
            outcome,
            closedByName,
            appointment.ClosedAtUtc is null ? null : _clock.ToLocal(appointment.ClosedAtUtc.Value));
    }

    private async Task<string?> ResolveClosedByNameAsync(string? closedByUserId)
    {
        if (string.IsNullOrWhiteSpace(closedByUserId))
            return null;

        var user = await _users.GetByIdAsync(closedByUserId);
        return user?.DisplayName;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildAuditDetails(Appointment appointment)
        => JsonSerializer.Serialize(new
        {
            appointment.StartsAtUtc,
            appointment.DurationMinutes,
            appointment.Status,
            appointment.Method
        });

    private sealed record ValidationResult(
        string? Error,
        string? FirstName,
        string? LastName,
        string? PhoneE164,
        string? HostUserId,
        string? CategoryId)
    {
        public static ValidationResult Fail(string error)
            => new(error, null, null, null, null, null);

        public static ValidationResult Ok(
            string firstName,
            string lastName,
            string phoneE164,
            string hostUserId,
            string categoryId)
            => new(null, firstName, lastName, phoneE164, hostUserId, categoryId);
    }
}
