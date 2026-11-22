using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Rooms;
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
    private const int MaxGuests = 10;
    private const int MaxHosts = 10;

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
    private readonly IMeetingRoomService _rooms;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AppointmentService(
        IAppointmentRepository appointments,
        IPersonRepository persons,
        IUserRepository users,
        ICategoryRepository categories,
        IMeetingRoomService rooms,
        IAuditLogger audit,
        IClock clock,
        ICurrentUser currentUser)
    {
        _appointments = appointments;
        _persons = persons;
        _users = users;
        _categories = categories;
        _rooms = rooms;
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
        string? hostUserId = null,
        string? roomId = null)
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
            status,
            NormalizeOptional(roomId)));

        return rows.Select(ToListItem).ToList();
    }

    public async Task<AppointmentDetailDto?> GetDetailAsync(string id)
    {
        var appointment = await _appointments.GetByIdAsync(id);
        if (appointment is null)
            return null;

        if (_currentUser.IsInRole(UserRole.Host) && !CanAccess(appointment))
            return null;

        var reminders = await _appointments.GetRemindersAsync(id);
        var closedByName = await ResolveClosedByNameAsync(appointment.ClosedByUserId);
        return await ToDetailAsync(appointment, reminders, closedByName);
    }

    private bool CanAccess(Appointment appointment)
        => appointment.HostUserId == _currentUser.UserId
           || appointment.Participants.Any(x => x.UserId == _currentUser.UserId);

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

        if (!string.IsNullOrWhiteSpace(input.ClientRequestId))
        {
            var existing = await _appointments.GetByClientRequestIdAsync(input.ClientRequestId);
            if (existing is not null)
                return AppointmentSaveResult.Ok(existing.Id);
        }

        var validation = await ValidateAsync(input, currentAppointmentId: null);
        if (validation.Error is not null)
            return AppointmentSaveResult.Fail(validation.Error);

        var startsAtUtc = _clock.ToUtc(input.StartsAtLocal);
        var endsAtUtc = startsAtUtc.AddMinutes(input.DurationMinutes);

        if (!input.IgnoreConflicts)
        {
            var conflicts = await FindConflictsAsync(
                validation.GuestPhones,
                validation.HostUserIds,
                validation.RoomId,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: null);

            if (conflicts.Count > 0)
                return AppointmentSaveResult.Conflict(BuildConflictMessage(conflicts), conflicts);
        }

        var resolvedGuests = await ResolveGuestsAsync(validation.Guests);
        var person = resolvedGuests.First(x => x.IsPrimary).Person;

        var now = _clock.UtcNow;
        var appointment = BuildEntity(
            person,
            input,
            startsAtUtc,
            now,
            validation.RoomId,
            validation.Hosts,
            resolvedGuests,
            clientRequestId: input.ClientRequestId);

        await _appointments.AddAsync(appointment);

        await _audit.LogAsync(
            AuditAction.AppointmentCreated,
            _currentUser.UserId,
            nameof(Appointment),
            appointment.Id,
            BuildAuditDetails(appointment));

        return AppointmentSaveResult.Ok(appointment.Id);
    }

    public async Task<SeriesPlanDto> PreviewSeriesAsync(AppointmentInput input, SeriesRequest request)
    {
        if (!CanManageAppointments())
            return new SeriesPlanDto(Array.Empty<SeriesSlotDto>(), "Seri oluşturma yetkiniz yok.");

        if (!request.IsValid)
            return new SeriesPlanDto(
                Array.Empty<SeriesSlotDto>(),
                $"Seri {SeriesScheduleCalculator.MinCount}–{SeriesScheduleCalculator.MaxCount} randevu arasında olmalıdır.");

        var validation = await ValidateAsync(input, currentAppointmentId: null);
        if (validation.Error is not null)
            return new SeriesPlanDto(Array.Empty<SeriesSlotDto>(), validation.Error);

        var slots = await BuildSeriesSlotsAsync(input, validation, request);
        return new SeriesPlanDto(slots);
    }

    public async Task<SeriesCreateResult> CreateSeriesAsync(AppointmentInput input, SeriesRequest request)
    {
        if (!CanManageAppointments())
            return SeriesCreateResult.Fail("Seri oluşturma yetkiniz yok.");

        if (!string.IsNullOrWhiteSpace(input.ClientRequestId))
        {
            var existing = await _appointments.GetByClientRequestIdAsync(input.ClientRequestId);
            if (existing?.SeriesId is not null)
            {
                var rows = await _appointments.GetSeriesAsync(existing.SeriesId);
                return SeriesCreateResult.Ok(
                    existing.SeriesId,
                    rows.Count,
                    0,
                    Array.Empty<SeriesSlotDto>());
            }
        }

        if (!request.IsValid)
            return SeriesCreateResult.Fail(
                $"Seri {SeriesScheduleCalculator.MinCount}–{SeriesScheduleCalculator.MaxCount} randevu arasında olmalıdır.");

        var validation = await ValidateAsync(input, currentAppointmentId: null);
        if (validation.Error is not null)
            return SeriesCreateResult.Fail(validation.Error);

        var slots = await BuildSeriesSlotsAsync(input, validation, request);
        var creatable = slots.Where(x => x.WillCreate).ToList();
        if (creatable.Count == 0)
            return SeriesCreateResult.Fail(
                "Seride oluşturulabilecek uygun tarih yok. Çakışan veya geçmiş tarihleri düzeltin.",
                slots);

        var resolvedGuests = await ResolveGuestsAsync(validation.Guests);
        var person = resolvedGuests.First(x => x.IsPrimary).Person;

        var now = _clock.UtcNow;
        var seriesId = Guid.NewGuid().ToString("N");
        var entities = new List<Appointment>(creatable.Count);

        foreach (var slot in creatable)
        {
            var startsAtUtc = _clock.ToUtc(slot.StartsAtLocal);
            entities.Add(BuildEntity(
                person,
                input,
                startsAtUtc,
                now,
                validation.RoomId,
                validation.Hosts,
                resolvedGuests,
                seriesId,
                slot.Index,
                slots.Count,
                request.Frequency,
                input.StartsAtLocal,
                clientRequestId: entities.Count == 0 ? input.ClientRequestId : null));
        }

        await _appointments.AddRangeAsync(entities);

        await _audit.LogAsync(
            AuditAction.SeriesCreated,
            _currentUser.UserId,
            nameof(Appointment),
            seriesId,
            JsonSerializer.Serialize(new
            {
                frequency = request.Frequency.ToString(),
                requested = request.Count,
                created = entities.Count,
                skipped = slots.Count - entities.Count,
                startsAtLocal = input.StartsAtLocal,
                hostUserId = input.HostUserId,
                categoryId = input.CategoryId
            }));

        return SeriesCreateResult.Ok(
            seriesId,
            entities.Count,
            slots.Count - entities.Count,
            slots);
    }

    public async Task<SeriesCancelResult> CancelSeriesAsync(string seriesId)
    {
        if (!CanManageAppointments())
            return SeriesCancelResult.Fail("Seriyi iptal etme yetkiniz yok.");

        if (string.IsNullOrWhiteSpace(seriesId))
            return SeriesCancelResult.Fail("Seri bulunamadı.");

        var rows = await _appointments.GetSeriesAsync(seriesId);
        if (rows.Count == 0)
            return SeriesCancelResult.Fail("Seri bulunamadı.");

        var now = _clock.UtcNow;
        var cancelled = 0;
        var skipped = 0;

        foreach (var appointment in rows)
        {
            if (appointment.IsDeleted
                || appointment.IsClosed
                || appointment.Status != AppointmentStatus.Scheduled
                || appointment.StartsAtUtc <= now)
            {
                skipped++;
                continue;
            }

            appointment.Cancel(now, _currentUser.UserId);
            await _appointments.SaveAsync(appointment, newReminders: null, cancelPendingReminders: true);
            cancelled++;
        }

        if (cancelled == 0)
            return SeriesCancelResult.Fail("İptal edilecek gelecek tarihli planlanmış görüşme bulunamadı.");

        await _audit.LogAsync(
            AuditAction.SeriesCancelled,
            _currentUser.UserId,
            nameof(Appointment),
            seriesId,
            JsonSerializer.Serialize(new { cancelled, skipped }));

        return SeriesCancelResult.Ok(cancelled, skipped);
    }

    public async Task<SeriesAppendPlanDto> PreviewAppendAsync(string seriesId, int count)
    {
        if (!CanManageAppointments())
            return new SeriesAppendPlanDto(Array.Empty<SeriesSlotDto>(), 0, "Seri düzenleme yetkiniz yok.");

        if (!new SeriesAppendRequest(count).IsValid)
            return new SeriesAppendPlanDto(
                Array.Empty<SeriesSlotDto>(),
                0,
                $"İşlem başına 1–{SeriesScheduleCalculator.MaxCount} kayıt eklenebilir.");

        var rows = await _appointments.GetSeriesAsync(seriesId);
        if (rows.Count == 0)
            return new SeriesAppendPlanDto(Array.Empty<SeriesSlotDto>(), 0, "Seri bulunamadı.");

        var context = ResolveSeriesContext(rows);
        var slots = await BuildAppendSlotsAsync(rows, context, count);

        return new SeriesAppendPlanDto(slots, context.RequestedCount + count);
    }

    public async Task<SeriesAppendResult> AppendSeriesAsync(string seriesId, int count)
    {
        if (!CanManageAppointments())
            return SeriesAppendResult.Fail("Seri düzenleme yetkiniz yok.");

        if (!new SeriesAppendRequest(count).IsValid)
            return SeriesAppendResult.Fail(
                $"İşlem başına 1–{SeriesScheduleCalculator.MaxCount} kayıt eklenebilir.");

        var rows = await _appointments.GetSeriesAsync(seriesId);
        if (rows.Count == 0)
            return SeriesAppendResult.Fail("Seri bulunamadı.");

        var context = ResolveSeriesContext(rows);
        await EnsureSeriesMetadataAsync(rows, context);

        var slots = await BuildAppendSlotsAsync(rows, context, count);
        var creatable = slots.Where(x => x.WillCreate).ToList();
        if (creatable.Count == 0)
            return SeriesAppendResult.Fail(
                "Eklenecek uygun tarih yok. Çakışan veya geçmiş tarihleri düzeltin.",
                slots);

        var person = rows[0].Person;
        if (person is null)
            return SeriesAppendResult.Fail("Seri kişisi bulunamadı.");

        var reminders = await _appointments.GetRemindersAsync(rows[0].Id);
        var offsets = reminders
            .Where(x => x.Kind == ReminderKind.Appointment)
            .Select(x => x.OffsetMinutes)
            .Distinct()
            .ToArray();

        var template = rows[0];
        var request = new AppointmentInput(
            person.FirstName,
            person.LastName,
            person.PhoneE164,
            _clock.ToLocal(template.StartsAtUtc),
            template.DurationMinutes,
            template.HostUserId,
            template.CategoryId,
            template.Method,
            template.Description,
            template.StudentFirstName,
            template.StudentLastName,
            offsets,
            RoomId: template.RoomId,
            ParticipantHostUserIds: template.Participants
                .Where(x => !x.IsPrimary)
                .Select(x => x.UserId)
                .ToList(),
            Guests: template.Guests
                .Select(x => new AppointmentGuestInput(
                    x.PersonId,
                    x.Person?.FirstName ?? string.Empty,
                    x.Person?.LastName ?? string.Empty,
                    x.Person?.PhoneE164 ?? string.Empty,
                    x.Person?.Email,
                    x.Person?.Organization,
                    x.IsPrimary))
                .ToList());

        var hosts = template.Participants
            .Select(x => new HostDraft(x.UserId, x.IsPrimary, x.SortOrder))
            .ToList();

        if (hosts.Count == 0)
            hosts.Add(new HostDraft(template.HostUserId, true, 0));

        var guests = template.Guests
            .Where(x => x.Person is not null)
            .Select(x => new ResolvedGuest(x.Person!, x.IsPrimary, x.SortOrder))
            .ToList();

        if (guests.Count == 0)
            guests.Add(new ResolvedGuest(person, true, 0));

        var newSeriesCount = context.RequestedCount + count;
        var now = _clock.UtcNow;
        var entities = new List<Appointment>(creatable.Count);

        foreach (var slot in creatable)
        {
            entities.Add(BuildEntity(
                person,
                request,
                _clock.ToUtc(slot.StartsAtLocal),
                now,
                template.RoomId,
                hosts,
                guests,
                seriesId,
                slot.Index,
                newSeriesCount,
                context.Frequency,
                context.AnchorLocal));
        }

        await _appointments.AddRangeAsync(entities);
        await _appointments.UpdateSeriesCountAsync(seriesId, newSeriesCount);

        await _audit.LogAsync(
            AuditAction.SeriesExtended,
            _currentUser.UserId,
            nameof(Appointment),
            seriesId,
            JsonSerializer.Serialize(new
            {
                requested = count,
                created = entities.Count,
                skipped = slots.Count - entities.Count,
                newSeriesCount
            }));

        return SeriesAppendResult.Ok(
            entities.Count,
            slots.Count - entities.Count,
            newSeriesCount,
            slots);
    }

    public async Task<SeriesUpdateResult> UpdateSeriesAsync(string seriesId, SeriesUpdateInput input)
    {
        if (!CanManageAppointments())
            return SeriesUpdateResult.Fail("Seri düzenleme yetkiniz yok.");

        if (input.DurationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
            return SeriesUpdateResult.Fail(
                $"Süre {MinDurationMinutes}–{MaxDurationMinutes} dakika arasında olmalıdır.");

        if (!Enum.IsDefined(input.Method))
            return SeriesUpdateResult.Fail("Geçersiz görüşme yöntemi.");

        if (input.ReminderOffsetsMinutes.Any(x => !ReminderScheduleCalculator.IsAllowedOffset(x)))
            return SeriesUpdateResult.Fail(
                $"Hatırlatıcı aralığı 1–{ReminderScheduleCalculator.MaxOffsetMinutes} dakika arasında olmalıdır.");

        var host = await _users.GetByIdAsync(input.HostUserId);
        if (host is null || !host.IsActive)
            return SeriesUpdateResult.Fail("Görüşmeci bulunamadı veya pasif.");

        var category = await _categories.GetByIdAsync(input.CategoryId);
        if (category is null)
            return SeriesUpdateResult.Fail("Kategori bulunamadı.");

        var hostUserIds = new List<string> { input.HostUserId };
        foreach (var rawId in input.ParticipantHostUserIds ?? Array.Empty<string>())
        {
            var participantId = rawId?.Trim();
            if (string.IsNullOrWhiteSpace(participantId)
                || hostUserIds.Contains(participantId, StringComparer.Ordinal))
            {
                continue;
            }

            var participant = await _users.GetByIdAsync(participantId);
            if (participant is null || !participant.IsActive)
                return SeriesUpdateResult.Fail("Katılımcı görüşmeci bulunamadı veya pasif.");

            hostUserIds.Add(participantId);
        }

        if (hostUserIds.Count > MaxHosts)
            return SeriesUpdateResult.Fail($"Bir görüşmeye en fazla {MaxHosts} görüşmeci eklenebilir.");

        var roomId = NormalizeOptional(input.RoomId);
        if (roomId is not null)
        {
            var room = await _rooms.GetByIdAsync(roomId);
            if (room is null || !room.IsActive)
                return SeriesUpdateResult.Fail("Seçilen oda bulunamadı veya pasif.");
        }
        else if (input.Method == AppointmentMethod.FaceToFace)
        {
            await _rooms.EnsureHostRoomAsync(input.HostUserId, host.DisplayName);
            roomId = (await _rooms.GetHostRoomAsync(input.HostUserId))?.Id;
            if (roomId is null)
                return SeriesUpdateResult.Fail("Görüşmecinin odası tanımlı değil.");
        }

        var rows = await _appointments.GetSeriesAsync(seriesId);
        if (rows.Count == 0)
            return SeriesUpdateResult.Fail("Seri bulunamadı.");

        var context = ResolveSeriesContext(rows);
        await EnsureSeriesMetadataAsync(rows, context);

        var now = _clock.UtcNow;
        var targets = rows
            .Where(x => !x.IsDeleted
                        && !x.IsClosed
                        && x.Status == AppointmentStatus.Scheduled
                        && x.StartsAtUtc > now)
            .ToList();

        if (targets.Count == 0)
            return SeriesUpdateResult.Fail("Güncellenecek gelecek tarihli planlanmış görüşme bulunamadı.");

        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        foreach (var appointment in targets)
        {
            var startsAtUtc = appointment.StartsAtUtc;

            if (input.TimeOfDay is not null)
            {
                var localDate = _clock.ToLocal(appointment.StartsAtUtc).Date;
                startsAtUtc = _clock.ToUtc(localDate.Add(input.TimeOfDay.Value));
            }

            var endsAtUtc = startsAtUtc.AddMinutes(input.DurationMinutes);

            var phones = appointment.Guests
                .Where(x => x.Person is not null)
                .Select(x => x.Person!.PhoneE164)
                .Append(appointment.Person?.PhoneE164 ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var conflicts = await FindConflictsAsync(
                phones,
                hostUserIds,
                roomId,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: appointment.Id);

            if (conflicts.Count > 0)
            {
                skipped++;
                if (warnings.Count < 5)
                {
                    warnings.Add(
                        $"{_clock.ToLocal(appointment.StartsAtUtc):dd/MM/yyyy HH:mm}: {BuildConflictMessage(conflicts)}");
                }

                continue;
            }

            var existingReminders = await _appointments.GetRemindersAsync(appointment.Id);
            var blockedAppointmentOffsets = existingReminders
                .Where(x => x.Kind == ReminderKind.Appointment && AlreadyNotifiedStates.Contains(x.State))
                .Select(x => x.OffsetMinutes)
                .ToHashSet();

            var blockedCallOffsets = existingReminders
                .Where(x => x.Kind == ReminderKind.Call && AlreadyNotifiedStates.Contains(x.State))
                .Select(x => x.OffsetMinutes)
                .ToHashSet();

            appointment.HostUserId = input.HostUserId;
            appointment.CategoryId = input.CategoryId;
            appointment.RoomId = roomId;
            appointment.StartsAtUtc = startsAtUtc;
            appointment.DurationMinutes = input.DurationMinutes;
            appointment.Method = input.Method;
            appointment.Description = NormalizeOptional(input.Description);
            appointment.StudentFirstName = NormalizeOptional(input.StudentFirstName);
            appointment.StudentLastName = NormalizeOptional(input.StudentLastName);
            appointment.UpdatedAtUtc = now;
            appointment.UpdatedByUserId = _currentUser.UserId;
            appointment.RowVersion++;

            appointment.Participants.Clear();
            for (var hostIndex = 0; hostIndex < hostUserIds.Count; hostIndex++)
            {
                appointment.Participants.Add(new AppointmentHost
                {
                    AppointmentId = appointment.Id,
                    UserId = hostUserIds[hostIndex],
                    IsPrimary = hostIndex == 0,
                    SortOrder = hostIndex
                });
            }

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
                })
                .ToList();

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
                })
                .ToList();

            newAppointmentReminders.AddRange(newCallReminders);

            await _appointments.SaveAsync(
                appointment,
                newAppointmentReminders,
                cancelPendingReminders: true);

            updated++;
        }

        if (updated == 0)
            return SeriesUpdateResult.Fail(
                warnings.Count == 0
                    ? "Seri güncellenemedi."
                    : $"Hiçbir kayıt güncellenemedi. {warnings[0]}");

        await _audit.LogAsync(
            AuditAction.SeriesUpdated,
            _currentUser.UserId,
            nameof(Appointment),
            seriesId,
            JsonSerializer.Serialize(new
            {
                updated,
                skipped,
                hostUserId = input.HostUserId,
                categoryId = input.CategoryId,
                durationMinutes = input.DurationMinutes,
                timeOfDay = input.TimeOfDay?.ToString()
            }));

        return SeriesUpdateResult.Ok(updated, skipped, warnings);
    }

    private sealed record SeriesContext(
        DateTime AnchorLocal,
        SeriesFrequency Frequency,
        int MaxIndex,
        int RequestedCount);

    private SeriesContext ResolveSeriesContext(IReadOnlyList<Appointment> rows)
    {
        var anchor = rows
            .Where(x => x.SeriesAnchorLocal is not null)
            .Select(x => x.SeriesAnchorLocal!.Value)
            .FirstOrDefault();

        if (anchor == default)
            anchor = rows.Min(x => _clock.ToLocal(x.StartsAtUtc));

        var frequency = rows
            .Where(x => x.SeriesFrequency is not null)
            .Select(x => x.SeriesFrequency!.Value)
            .FirstOrDefault();

        if (!Enum.IsDefined(frequency) || frequency == default && rows.All(x => x.SeriesFrequency is null))
        {
            var localDates = rows
                .OrderBy(x => x.StartsAtUtc)
                .Select(x => _clock.ToLocal(x.StartsAtUtc))
                .ToList();

            frequency = localDates.Count >= 2
                ? (localDates[1].Date - localDates[0].Date).Days switch
                {
                    7 => SeriesFrequency.Weekly,
                    14 => SeriesFrequency.Biweekly,
                    _ => SeriesFrequency.Monthly
                }
                : SeriesFrequency.Weekly;
        }

        var maxIndex = rows.Max(x => x.SeriesIndex ?? 1);
        var requested = rows.Max(x => x.SeriesCount ?? maxIndex);

        return new SeriesContext(anchor, frequency, maxIndex, Math.Max(requested, maxIndex));
    }

    private async Task EnsureSeriesMetadataAsync(
        IReadOnlyList<Appointment> rows,
        SeriesContext context)
    {
        if (rows.All(x => x.SeriesFrequency is not null && x.SeriesAnchorLocal is not null))
            return;

        var seriesId = rows[0].SeriesId;
        if (string.IsNullOrWhiteSpace(seriesId))
            return;

        await _appointments.SetSeriesMetadataAsync(seriesId, context.Frequency, context.AnchorLocal);
    }

    private async Task<IReadOnlyList<SeriesSlotDto>> BuildAppendSlotsAsync(
        IReadOnlyList<Appointment> rows,
        SeriesContext context,
        int count)
    {
        var now = _clock.UtcNow;
        var phones = rows[0].Guests
            .Where(x => x.Person is not null)
            .Select(x => x.Person!.PhoneE164)
            .Append(rows[0].Person?.PhoneE164 ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hostUserIds = rows[0].Participants
            .Select(x => x.UserId)
            .Append(rows[0].HostUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var roomId = rows[0].RoomId;
        var duration = rows[0].DurationMinutes;
        var slots = new List<SeriesSlotDto>(count);

        for (var offset = 1; offset <= count; offset++)
        {
            var index = context.MaxIndex + offset;
            var startsAtLocal = SeriesScheduleCalculator.Slot(context.AnchorLocal, context.Frequency, index);
            var endsAtLocal = startsAtLocal.AddMinutes(duration);
            var startsAtUtc = _clock.ToUtc(startsAtLocal);
            var endsAtUtc = startsAtUtc.AddMinutes(duration);

            if (startsAtUtc <= now)
            {
                slots.Add(new SeriesSlotDto(
                    index,
                    startsAtLocal,
                    endsAtLocal,
                    SeriesSlotState.Past,
                    "Geçmiş tarih; oluşturulmaz.",
                    Array.Empty<AppointmentConflictDto>()));
                continue;
            }

            var conflicts = await FindConflictsAsync(
                phones,
                hostUserIds,
                roomId,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: null);

            slots.Add(conflicts.Count == 0
                ? new SeriesSlotDto(
                    index,
                    startsAtLocal,
                    endsAtLocal,
                    SeriesSlotState.Create,
                    null,
                    conflicts)
                : new SeriesSlotDto(
                    index,
                    startsAtLocal,
                    endsAtLocal,
                    SeriesSlotState.Conflict,
                    BuildConflictMessage(conflicts),
                    conflicts));
        }

        return slots;
    }

    private async Task<IReadOnlyList<SeriesSlotDto>> BuildSeriesSlotsAsync(
        AppointmentInput input,
        ValidationResult validation,
        SeriesRequest request)
    {
        var now = _clock.UtcNow;
        var dates = SeriesScheduleCalculator.Build(input.StartsAtLocal, request.Frequency, request.Count);
        var slots = new List<SeriesSlotDto>(dates.Count);

        for (var index = 0; index < dates.Count; index++)
        {
            var startsAtLocal = dates[index];
            var endsAtLocal = startsAtLocal.AddMinutes(input.DurationMinutes);
            var startsAtUtc = _clock.ToUtc(startsAtLocal);
            var endsAtUtc = startsAtUtc.AddMinutes(input.DurationMinutes);

            if (startsAtUtc <= now)
            {
                slots.Add(new SeriesSlotDto(
                    index + 1,
                    startsAtLocal,
                    endsAtLocal,
                    SeriesSlotState.Past,
                    "Geçmiş tarih; oluşturulmaz.",
                    Array.Empty<AppointmentConflictDto>()));
                continue;
            }

            var conflicts = await FindConflictsAsync(
                validation.GuestPhones,
                validation.HostUserIds,
                validation.RoomId,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: null);

            slots.Add(conflicts.Count == 0
                ? new SeriesSlotDto(
                    index + 1,
                    startsAtLocal,
                    endsAtLocal,
                    SeriesSlotState.Create,
                    null,
                    conflicts)
                : new SeriesSlotDto(
                    index + 1,
                    startsAtLocal,
                    endsAtLocal,
                    SeriesSlotState.Conflict,
                    BuildConflictMessage(conflicts),
                    conflicts));
        }

        return slots;
    }

    private Appointment BuildEntity(
        Person person,
        AppointmentInput input,
        DateTime startsAtUtc,
        DateTime now,
        string? roomId,
        IReadOnlyList<HostDraft> hosts,
        IReadOnlyList<ResolvedGuest> guests,
        string? seriesId = null,
        int? seriesIndex = null,
        int? seriesCount = null,
        SeriesFrequency? seriesFrequency = null,
        DateTime? seriesAnchorLocal = null,
        string? clientRequestId = null)
    {
        var appointment = new Appointment
        {
            PersonId = person.Id,
            HostUserId = input.HostUserId,
            CategoryId = input.CategoryId,
            RoomId = roomId,
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
            ClientRequestId = clientRequestId,
            RowVersion = 1,
            SeriesId = seriesId,
            SeriesIndex = seriesIndex,
            SeriesCount = seriesCount,
            SeriesFrequency = seriesFrequency,
            SeriesAnchorLocal = seriesAnchorLocal
        };

        foreach (var host in hosts)
        {
            appointment.Participants.Add(new AppointmentHost
            {
                AppointmentId = appointment.Id,
                UserId = host.UserId,
                IsPrimary = host.IsPrimary,
                SortOrder = host.SortOrder
            });
        }

        foreach (var guest in guests)
        {
            appointment.Guests.Add(new AppointmentGuest
            {
                AppointmentId = appointment.Id,
                PersonId = guest.Person.Id,
                IsPrimary = guest.IsPrimary,
                SortOrder = guest.SortOrder
            });
        }

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

        return appointment;
    }

    public async Task<AppointmentSaveResult> UpdateAsync(string id, AppointmentInput input)
    {
        if (!CanManageAppointments())
            return AppointmentSaveResult.Fail("Görüşme düzenleme yetkiniz yok.");

        var appointment = await _appointments.GetByIdAsync(id);
        if (appointment is null || appointment.IsDeleted)
            return AppointmentSaveResult.Fail("Görüşme bulunamadı.");

        if (input.ExpectedUpdatedAtUtc is { } expected
            && Math.Abs((appointment.UpdatedAtUtc - expected).TotalSeconds) > 1)
        {
            var current = await GetDetailAsync(id);
            return current is null
                ? AppointmentSaveResult.Fail("Görüşme bulunamadı.")
                : AppointmentSaveResult.VersionConflictResult(
                    "Görüşme siz düzenlerken başka bir kullanıcı tarafından değiştirildi.",
                    current);
        }

        var validation = await ValidateAsync(input, currentAppointmentId: id);
        if (validation.Error is not null)
            return AppointmentSaveResult.Fail(validation.Error);

        var startsAtUtc = _clock.ToUtc(input.StartsAtLocal);
        var endsAtUtc = startsAtUtc.AddMinutes(input.DurationMinutes);

        if (!input.IgnoreConflicts)
        {
            var conflicts = await FindConflictsAsync(
                validation.GuestPhones,
                validation.HostUserIds,
                validation.RoomId,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId: id);

            if (conflicts.Count > 0)
                return AppointmentSaveResult.Conflict(BuildConflictMessage(conflicts), conflicts);
        }

        var resolvedGuests = await ResolveGuestsAsync(validation.Guests);
        var person = resolvedGuests.First(x => x.IsPrimary).Person;

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
        appointment.RoomId = validation.RoomId;
        appointment.StartsAtUtc = startsAtUtc;
        appointment.DurationMinutes = input.DurationMinutes;
        appointment.Method = input.Method;
        appointment.Description = NormalizeOptional(input.Description);
        appointment.StudentFirstName = NormalizeOptional(input.StudentFirstName);
        appointment.StudentLastName = NormalizeOptional(input.StudentLastName);
        appointment.UpdatedAtUtc = now;
        appointment.UpdatedByUserId = _currentUser.UserId;
        appointment.RowVersion++;

        appointment.Participants.Clear();
        foreach (var host in validation.Hosts)
        {
            appointment.Participants.Add(new AppointmentHost
            {
                AppointmentId = appointment.Id,
                UserId = host.UserId,
                IsPrimary = host.IsPrimary,
                SortOrder = host.SortOrder
            });
        }

        var existingGuests = appointment.Guests
            .GroupBy(x => x.PersonId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        appointment.Guests.Clear();
        foreach (var guest in resolvedGuests)
        {
            existingGuests.TryGetValue(guest.Person.Id, out var existing);
            appointment.Guests.Add(new AppointmentGuest
            {
                Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
                AppointmentId = appointment.Id,
                PersonId = guest.Person.Id,
                IsPrimary = guest.IsPrimary,
                SortOrder = guest.SortOrder,
                Attendance = existing?.Attendance,
                Summary = existing?.Summary,
                PostNotes = existing?.PostNotes,
                WillRepeat = existing?.WillRepeat ?? false,
                IsClosed = existing?.IsClosed ?? false,
                ClosedAtUtc = existing?.ClosedAtUtc,
                ClosedByUserId = existing?.ClosedByUserId,
                UpdatedAtUtc = existing?.UpdatedAtUtc ?? now,
                UpdatedByUserId = existing?.UpdatedByUserId,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now
            });
        }

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

    public async Task<int> AnonymizeOldAppointmentsAsync(int retentionYears = 2)
    {
        var years = Math.Clamp(retentionYears, 1, 10);
        var now = _clock.UtcNow;
        var changed = await _appointments.AnonymizeBeforeAsync(now.AddYears(-years), now);

        if (changed > 0)
        {
            await _audit.LogAsync(
                AuditAction.AppointmentAnonymized,
                userId: null,
                entityType: nameof(Appointment),
                detailsJson: JsonSerializer.Serialize(new { changed, retentionYears = years }));
        }

        return changed;
    }

    private bool CanManageAppointments()
        => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    private string? ScopedHostUserId()
        => _currentUser.IsInRole(UserRole.Host) ? _currentUser.UserId : null;

    private async Task<ValidationResult> ValidateAsync(
        AppointmentInput input,
        string? currentAppointmentId)
    {
        if (input.StartsAtLocal == default)
            return ValidationResult.Fail("Görüşme tarihi ve saati zorunludur.");

        if (input.DurationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
            return ValidationResult.Fail(
                $"Görüşme süresi {MinDurationMinutes}–{MaxDurationMinutes} dakika arasında olmalıdır.");

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

        var hostUserId = input.HostUserId?.Trim() ?? string.Empty;
        if (hostUserId.Length == 0)
            return ValidationResult.Fail("Görüşülecek kişi (host) seçilmelidir.");

        var host = await _users.GetByIdAsync(hostUserId);
        if (host is null || !host.IsActive)
            return ValidationResult.Fail("Seçilen host bulunamadı veya pasif.");

        var hosts = new List<HostDraft> { new(hostUserId, true, 0) };
        foreach (var rawId in input.ParticipantHostUserIds ?? Array.Empty<string>())
        {
            var participantId = rawId?.Trim();
            if (string.IsNullOrWhiteSpace(participantId)
                || hosts.Any(x => string.Equals(x.UserId, participantId, StringComparison.Ordinal)))
            {
                continue;
            }

            if (hosts.Count >= MaxHosts)
                return ValidationResult.Fail($"Bir görüşmeye en fazla {MaxHosts} görüşmeci eklenebilir.");

            var participant = await _users.GetByIdAsync(participantId);
            if (participant is null || !participant.IsActive)
                return ValidationResult.Fail("Katılımcı görüşmeci bulunamadı veya pasif.");

            hosts.Add(new HostDraft(participantId, false, hosts.Count));
        }

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

        var guests = new List<GuestDraft>();
        if (input.Guests is { Count: > 0 })
        {
            if (input.Guests.Count > MaxGuests)
                return ValidationResult.Fail($"Bir görüşmeye en fazla {MaxGuests} misafir eklenebilir.");

            foreach (var guest in input.Guests)
            {
                var error = await TryBuildGuestDraftAsync(guest, guests);
                if (error is not null)
                    return ValidationResult.Fail(error);
            }

            var duplicate = guests
                .GroupBy(x => string.IsNullOrWhiteSpace(x.PersonId) ? x.PhoneE164 : x.PersonId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate is not null)
                return ValidationResult.Fail("Aynı misafir bir görüşmeye birden fazla kez eklenemez.");

            NormalizePrimaryGuest(guests);
        }
        else
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

            guests.Add(new GuestDraft(
                null,
                firstName,
                lastName,
                phoneE164,
                null,
                null,
                true));
        }

        var roomId = NormalizeOptional(input.RoomId);
        if (roomId is not null)
        {
            var room = await _rooms.GetByIdAsync(roomId);
            if (room is null || !room.IsActive)
                return ValidationResult.Fail("Seçilen oda bulunamadı veya pasif.");
        }
        else if (input.Method == AppointmentMethod.FaceToFace)
        {
            await _rooms.EnsureHostRoomAsync(hostUserId, host.DisplayName);
            roomId = (await _rooms.GetHostRoomAsync(hostUserId))?.Id;
            if (roomId is null)
                return ValidationResult.Fail("Görüşmecinin odası tanımlı değil.");
        }

        return ValidationResult.Ok(guests, hosts, input.CategoryId, roomId);
    }

    private async Task<string?> TryBuildGuestDraftAsync(
        AppointmentGuestInput input,
        List<GuestDraft> target)
    {
        var personId = NormalizeOptional(input.PersonId);
        Person? person = null;

        if (personId is not null)
        {
            person = await _persons.GetByIdAsync(personId);
            if (person is null)
                return "Seçilen misafir bulunamadı.";
        }

        var firstName = input.FirstName?.Trim() ?? person?.FirstName ?? string.Empty;
        var lastName = input.LastName?.Trim() ?? person?.LastName ?? string.Empty;

        if (firstName.Length == 0)
            return "Misafir adı zorunludur.";

        if (firstName.Length > MaxNameLength)
            return $"Misafir adı en fazla {MaxNameLength} karakter olabilir.";

        if (lastName.Length == 0)
            return "Misafir soyadı zorunludur.";

        if (lastName.Length > MaxNameLength)
            return $"Misafir soyadı en fazla {MaxNameLength} karakter olabilir.";

        var phone = input.Phone;
        if (!PhoneNumber.TryNormalize(phone, out var phoneE164))
        {
            if (person is null)
                return "Misafir telefon numarası geçerli değil.";

            phoneE164 = person.PhoneE164;
        }

        target.Add(new GuestDraft(
            personId,
            firstName,
            lastName,
            phoneE164,
            NormalizeOptional(input.Email) ?? person?.Email,
            NormalizeOptional(input.Organization) ?? person?.Organization,
            input.IsPrimary));

        return null;
    }

    private static void NormalizePrimaryGuest(List<GuestDraft> guests)
    {
        var primaryIndex = guests.FindIndex(x => x.IsPrimary);
        if (primaryIndex < 0)
            primaryIndex = 0;

        for (var i = 0; i < guests.Count; i++)
            guests[i] = guests[i] with { IsPrimary = i == primaryIndex };
    }

    private async Task<IReadOnlyList<ResolvedGuest>> ResolveGuestsAsync(
        IReadOnlyList<GuestDraft> drafts)
    {
        var result = new List<ResolvedGuest>(drafts.Count);

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            Person? person = null;

            if (!string.IsNullOrWhiteSpace(draft.PersonId))
                person = await _persons.GetByIdAsync(draft.PersonId);

            if (person is null)
            {
                person = await _persons.FindByPhoneAndNameAsync(
                    draft.PhoneE164,
                    draft.FirstName,
                    draft.LastName);

                if (person is null)
                {
                    person = new Person
                    {
                        FirstName = draft.FirstName,
                        LastName = draft.LastName,
                        PhoneE164 = draft.PhoneE164,
                        Email = draft.Email,
                        Organization = draft.Organization
                    };

                    await _persons.AddAsync(person);
                }
            }
            else
            {
                var changed = false;
                if (!string.IsNullOrWhiteSpace(draft.Email) && person.Email != draft.Email)
                {
                    person.Email = draft.Email;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(draft.Organization)
                    && person.Organization != draft.Organization)
                {
                    person.Organization = draft.Organization;
                    changed = true;
                }

                if (changed)
                    await _persons.SaveAsync(person);
            }

            result.Add(new ResolvedGuest(person, draft.IsPrimary, i));
        }

        return result;
    }

    private async Task<IReadOnlyList<AppointmentConflictDto>> FindConflictsAsync(
        IReadOnlyCollection<string> guestPhones,
        IReadOnlyCollection<string> hostUserIds,
        string? roomId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId)
    {
        var phones = guestPhones
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<AppointmentConflictDto>();

        var hostRecords = await _appointments.FindHostConflictsAsync(
            hostUserIds,
            startsAtUtc,
            endsAtUtc,
            excludeAppointmentId);

        result.AddRange(hostRecords.Select(x =>
            MapConflict(x, ConflictKind.Host, x.HostName, phones)));

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            var roomRecords = await _appointments.FindRoomConflictsAsync(
                roomId,
                startsAtUtc,
                endsAtUtc,
                excludeAppointmentId);

            result.AddRange(roomRecords.Select(x =>
                MapConflict(x, ConflictKind.Room, x.RoomName, phones)));
        }

        var guestRecords = await _appointments.FindGuestConflictsAsync(
            phones,
            startsAtUtc,
            endsAtUtc,
            excludeAppointmentId);

        result.AddRange(guestRecords.Select(x =>
            MapConflict(x, ConflictKind.Guest, x.GuestName, phones)));

        return result;
    }

    private AppointmentConflictDto MapConflict(
        AppointmentConflictRecord record,
        ConflictKind kind,
        string? resourceName,
        IReadOnlySet<string> phones)
        => new(
            record.AppointmentId,
            $"{record.PersonFirstName} {record.PersonLastName}".Trim(),
            _clock.ToLocal(record.StartsAtUtc),
            _clock.ToLocal(record.EndsAtUtc),
            record.CategoryName,
            phones.Contains(record.PhoneE164),
            kind,
            resourceName);

    private static string BuildConflictMessage(IReadOnlyList<AppointmentConflictDto> conflicts)
    {
        var first = conflicts[0];
        var samePerson = conflicts.Any(x => x.IsSamePerson);
        var prefix = samePerson
            ? "Aynı kişi için çakışan bir görüşme var"
            : first.Kind switch
            {
                ConflictKind.Room => $"{first.ResourceName ?? "Oda"} bu saatte dolu",
                ConflictKind.Guest => $"{first.ResourceName ?? first.PersonName} bu saatte başka bir görüşmede",
                _ => $"{first.ResourceName ?? "Görüşmeci"} bu saatte başka bir görüşmede"
            };

        var extra = conflicts.Count > 1 ? $" (+{conflicts.Count - 1} çakışma)" : string.Empty;
        return $"{prefix}{extra}: {first.StartsAtLocal:dd/MM/yyyy HH:mm} · {first.PersonName}. Yine de kaydedilsin mi?";
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
        var participants = BuildParticipantDtos(appointment);
        var guests = BuildGuestDtos(appointment, new Dictionary<string, string>());
        var primaryHost = participants.FirstOrDefault(x => x.IsPrimary) ?? participants.FirstOrDefault();

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
            NullIfEmpty(appointment.StudentFullName),
            RoomId: appointment.RoomId,
            RoomName: appointment.Room?.Name,
            HostTitleName: primaryHost?.TitleName,
            ParticipantCount: participants.Count,
            GuestCount: guests.Count,
            ParticipantNames: participants.Count == 0
                ? null
                : string.Join(", ", participants.Select(x => x.DisplayName)),
            GuestNames: guests.Count == 0
                ? null
                : string.Join(", ", guests.Select(x => x.FullName)));
    }

    private async Task<AppointmentDetailDto> ToDetailAsync(
        Appointment appointment,
        IReadOnlyList<ReminderDispatch> reminders,
        string? closedByName)
    {
        var startsAtLocal = _clock.ToLocal(appointment.StartsAtUtc);
        var participants = BuildParticipantDtos(appointment);

        var guestClosedByNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var userId in appointment.Guests
                     .Where(x => !string.IsNullOrWhiteSpace(x.ClosedByUserId))
                     .Select(x => x.ClosedByUserId!)
                     .Distinct(StringComparer.Ordinal))
        {
            var user = await _users.GetByIdAsync(userId);
            if (user is not null)
                guestClosedByNames[userId] = user.DisplayName;
        }

        var guests = BuildGuestDtos(appointment, guestClosedByNames);

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
                _clock.ToLocal(appointment.Outcome.UpdatedAtUtc),
                guests);

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
            appointment.ClosedAtUtc is null ? null : _clock.ToLocal(appointment.ClosedAtUtc.Value),
            appointment.SeriesId,
            appointment.SeriesIndex,
            appointment.SeriesCount,
            appointment.UpdatedAtUtc,
            RoomId: appointment.RoomId,
            RoomName: appointment.Room?.Name,
            RoomKind: appointment.Room?.Kind,
            Participants: participants,
            Guests: guests);
    }

    private List<AppointmentParticipantDto> BuildParticipantDtos(Appointment appointment)
    {
        if (appointment.Participants.Count > 0)
        {
            return appointment.Participants
                .OrderBy(x => x.SortOrder)
                .Select(x => new AppointmentParticipantDto(
                    x.UserId,
                    x.User?.DisplayName
                    ?? (x.UserId == appointment.HostUserId ? appointment.HostUser?.DisplayName : null)
                    ?? string.Empty,
                    x.User?.Title?.Name
                    ?? (x.UserId == appointment.HostUserId ? appointment.HostUser?.Title?.Name : null),
                    x.IsPrimary))
                .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
                .ToList();
        }

        return new List<AppointmentParticipantDto>
        {
            new(
                appointment.HostUserId,
                appointment.HostUser?.DisplayName ?? string.Empty,
                appointment.HostUser?.Title?.Name,
                true)
        };
    }

    private List<AppointmentGuestDto> BuildGuestDtos(
        Appointment appointment,
        IReadOnlyDictionary<string, string> closedByNames)
    {
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
                appointment.Outcome is null
                    ? null
                    : new AppointmentGuestOutcomeDto(
                        appointment.Outcome.Attendance,
                        appointment.Outcome.Summary,
                        appointment.Outcome.WillRepeat,
                        appointment.Outcome.PostNotes,
                        appointment.Outcome.IsClosed,
                        appointment.Outcome.ClosedAtUtc is null
                            ? null
                            : _clock.ToLocal(appointment.Outcome.ClosedAtUtc.Value),
                        null,
                        _clock.ToLocal(appointment.Outcome.UpdatedAtUtc))));
        }

        return guests;
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
            appointment.Method,
            appointment.RoomId,
            hostUserIds = appointment.Participants.Select(x => x.UserId).ToList(),
            guestPersonIds = appointment.Guests.Select(x => x.PersonId).ToList()
        });

    private sealed record GuestDraft(
        string? PersonId,
        string FirstName,
        string LastName,
        string PhoneE164,
        string? Email,
        string? Organization,
        bool IsPrimary);

    private sealed record HostDraft(
        string UserId,
        bool IsPrimary,
        int SortOrder);

    private sealed record ResolvedGuest(
        Person Person,
        bool IsPrimary,
        int SortOrder);

    private sealed record ValidationResult(
        string? Error,
        IReadOnlyList<GuestDraft> Guests,
        IReadOnlyList<HostDraft> Hosts,
        string? CategoryId,
        string? RoomId)
    {
        public string? FirstName => Guests.FirstOrDefault(x => x.IsPrimary)?.FirstName;

        public string? LastName => Guests.FirstOrDefault(x => x.IsPrimary)?.LastName;

        public string? PhoneE164 => Guests.FirstOrDefault(x => x.IsPrimary)?.PhoneE164;

        public string? HostUserId => Hosts.FirstOrDefault(x => x.IsPrimary)?.UserId;

        public IReadOnlyList<string> HostUserIds => Hosts.Select(x => x.UserId).ToList();

        public IReadOnlyList<string> GuestPhones => Guests
            .Select(x => x.PhoneE164)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        public static ValidationResult Fail(string error)
            => new(error, Array.Empty<GuestDraft>(), Array.Empty<HostDraft>(), null, null);

        public static ValidationResult Ok(
            IReadOnlyList<GuestDraft> guests,
            IReadOnlyList<HostDraft> hosts,
            string categoryId,
            string? roomId)
            => new(null, guests, hosts, categoryId, roomId);
    }
}
