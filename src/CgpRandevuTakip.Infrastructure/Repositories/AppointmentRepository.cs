using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class AppointmentRepository : IAppointmentRepository
{
    private const int MaxDurationMinutes = 240;

    private readonly IDbContextFactory<AppDbContext> _factory;

    public AppointmentRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Appointment?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser).ThenInclude(x => x!.Title)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Outcome)
            .Include(x => x.Participants).ThenInclude(x => x.User).ThenInclude(x => x!.Title)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Appointment?> GetByClientRequestIdAsync(string clientRequestId)
    {
        if (string.IsNullOrWhiteSpace(clientRequestId))
            return null;

        await using var db = await _factory.CreateDbContextAsync();
        return await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser).ThenInclude(x => x!.Title)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Outcome)
            .Include(x => x.Participants).ThenInclude(x => x.User).ThenInclude(x => x!.Title)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .FirstOrDefaultAsync(x => x.ClientRequestId == clientRequestId);
    }

    public async Task<AppointmentOutcome?> GetOutcomeAsync(string appointmentId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AppointmentOutcomes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);
    }

    public async Task<Appointment?> GetNextAsync(DateTime nowUtc, string? hostUserId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser).ThenInclude(x => x!.Title)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .Where(x => !x.IsDeleted
                        && !x.IsClosed
                        && x.StartsAtUtc >= nowUtc
                        && (x.Status == AppointmentStatus.Scheduled
                            || x.Status == AppointmentStatus.Arrived));

        if (!string.IsNullOrWhiteSpace(hostUserId))
            query = query.Where(x =>
                x.HostUserId == hostUserId
                || x.Participants.Any(p => p.UserId == hostUserId));

        return await query
            .OrderBy(x => x.StartsAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Appointment>> QueryAsync(AppointmentQuery query)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var rows = db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser).ThenInclude(x => x!.Title)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Participants).ThenInclude(x => x.User).ThenInclude(x => x!.Title)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .Where(x => !x.IsDeleted);

        if (query.FromUtc is not null)
            rows = rows.Where(x => x.StartsAtUtc >= query.FromUtc.Value);

        if (query.ToUtc is not null)
            rows = rows.Where(x => x.StartsAtUtc < query.ToUtc.Value);

        if (query.Status is not null)
            rows = rows.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.CategoryId))
            rows = rows.Where(x => x.CategoryId == query.CategoryId);

        if (!string.IsNullOrWhiteSpace(query.HostUserId))
            rows = rows.Where(x =>
                x.HostUserId == query.HostUserId
                || x.Participants.Any(p => p.UserId == query.HostUserId));

        if (!string.IsNullOrWhiteSpace(query.RoomId))
            rows = rows.Where(x => x.RoomId == query.RoomId);

        var list = await rows
            .OrderBy(x => x.StartsAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync();

        if (string.IsNullOrWhiteSpace(query.SearchText))
            return list;

        var term = query.SearchText.Trim();
        var digits = new string(term.Where(char.IsDigit).ToArray());

        return list
            .Where(x => MatchesText(x, term, digits))
            .ToList();
    }

    public async Task<IReadOnlyList<AppointmentConflictRecord>> FindHostConflictsAsync(
        IReadOnlyCollection<string> hostUserIds,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId)
    {
        var ids = hostUserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return Array.Empty<AppointmentConflictRecord>();

        await using var db = await _factory.CreateDbContextAsync();

        var windowStart = startsAtUtc.AddMinutes(-MaxDurationMinutes);

        var rows = await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Participants).ThenInclude(x => x.User)
            .Where(x => !x.IsDeleted
                        && (ids.Contains(x.HostUserId)
                            || x.Participants.Any(p => ids.Contains(p.UserId)))
                        && x.StartsAtUtc > windowStart
                        && x.StartsAtUtc < endsAtUtc
                        && (excludeAppointmentId == null || x.Id != excludeAppointmentId)
                        && (x.Status == AppointmentStatus.Scheduled
                            || x.Status == AppointmentStatus.Arrived
                            || x.Status == AppointmentStatus.Postponed))
            .ToListAsync();

        return rows
            .Where(x => Overlaps(x, startsAtUtc, endsAtUtc))
            .Select(x =>
            {
                var matchedId = ids.FirstOrDefault(id =>
                    x.HostUserId == id || x.Participants.Any(p => p.UserId == id));
                var hostName = x.Participants
                                   .FirstOrDefault(p => p.UserId == matchedId)?.User?.DisplayName
                               ?? x.HostUser?.DisplayName;
                return MapConflict(x, hostName: hostName);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AppointmentConflictRecord>> FindRoomConflictsAsync(
        string roomId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return Array.Empty<AppointmentConflictRecord>();

        await using var db = await _factory.CreateDbContextAsync();

        var windowStart = startsAtUtc.AddMinutes(-MaxDurationMinutes);

        var rows = await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Where(x => !x.IsDeleted
                        && x.RoomId == roomId
                        && x.StartsAtUtc > windowStart
                        && x.StartsAtUtc < endsAtUtc
                        && (excludeAppointmentId == null || x.Id != excludeAppointmentId)
                        && (x.Status == AppointmentStatus.Scheduled
                            || x.Status == AppointmentStatus.Arrived
                            || x.Status == AppointmentStatus.Postponed))
            .ToListAsync();

        return rows
            .Where(x => Overlaps(x, startsAtUtc, endsAtUtc))
            .Select(x => MapConflict(x, hostName: x.HostUser?.DisplayName))
            .ToList();
    }

    public async Task<IReadOnlyList<AppointmentConflictRecord>> FindGuestConflictsAsync(
        IReadOnlyCollection<string> phoneE164s,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId)
    {
        var phones = phoneE164s
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (phones.Count == 0)
            return Array.Empty<AppointmentConflictRecord>();

        await using var db = await _factory.CreateDbContextAsync();

        var windowStart = startsAtUtc.AddMinutes(-MaxDurationMinutes);

        var rows = await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .Where(x => !x.IsDeleted
                        && ((x.Person != null && phones.Contains(x.Person.PhoneE164))
                            || x.Guests.Any(g => g.Person != null
                                                 && phones.Contains(g.Person.PhoneE164)))
                        && x.StartsAtUtc > windowStart
                        && x.StartsAtUtc < endsAtUtc
                        && (excludeAppointmentId == null || x.Id != excludeAppointmentId)
                        && (x.Status == AppointmentStatus.Scheduled
                            || x.Status == AppointmentStatus.Arrived
                            || x.Status == AppointmentStatus.Postponed))
            .ToListAsync();

        return rows
            .Where(x => Overlaps(x, startsAtUtc, endsAtUtc))
            .Select(x =>
            {
                var guestName = x.Guests
                    .FirstOrDefault(g => g.Person != null
                                         && phones.Contains(g.Person.PhoneE164))?.Person?.FullName;
                return MapConflict(
                    x,
                    hostName: x.HostUser?.DisplayName,
                    guestName: guestName);
            })
            .ToList();
    }

    private static bool Overlaps(Appointment appointment, DateTime startsAtUtc, DateTime endsAtUtc)
        => appointment.StartsAtUtc < endsAtUtc
           && appointment.StartsAtUtc.AddMinutes(appointment.DurationMinutes) > startsAtUtc;

    private static AppointmentConflictRecord MapConflict(
        Appointment appointment,
        string? hostName = null,
        string? guestName = null)
        => new(
            appointment.Id,
            appointment.Person?.FirstName ?? string.Empty,
            appointment.Person?.LastName ?? string.Empty,
            appointment.Person?.PhoneE164 ?? string.Empty,
            appointment.StartsAtUtc,
            appointment.StartsAtUtc.AddMinutes(appointment.DurationMinutes),
            appointment.Category?.Name,
            hostName,
            appointment.Room?.Name,
            guestName);

    public async Task<IReadOnlyList<ReminderDispatch>> GetRemindersAsync(string appointmentId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ReminderDispatches.AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.DueAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReminderDispatch>> GetDueRemindersAsync(DateTime nowUtc, int batchSize)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ReminderDispatches.AsNoTracking()
            .Include(x => x.Appointment!)
                .ThenInclude(a => a.Person)
            .Include(x => x.Appointment!)
                .ThenInclude(a => a.HostUser)
            .Include(x => x.Appointment!)
                .ThenInclude(a => a.Category)
            .Where(x => x.State == ReminderState.Pending
                        && x.DueAtUtc <= nowUtc
                        && x.Appointment != null
                        && !x.Appointment.IsDeleted
                        && !x.Appointment.IsClosed
                        && (x.Appointment.Status == AppointmentStatus.Scheduled
                            || x.Appointment.Status == AppointmentStatus.Arrived))
            .OrderBy(x => x.DueAtUtc)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReminderDispatch>> GetExpiredSentRemindersAsync(DateTime cutoffUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ReminderDispatches.AsNoTracking()
            .Where(x => x.State == ReminderState.Sent
                        && x.ActionAtUtc == null
                        && x.WindowsNotifiedAtUtc != null
                        && x.WindowsNotifiedAtUtc <= cutoffUtc)
            .OrderBy(x => x.WindowsNotifiedAtUtc)
            .ToListAsync();
    }

    public async Task<ReminderDispatch?> GetReminderAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ReminderDispatches.AsNoTracking()
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task MarkWindowsNotifiedAsync(string id, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id && x.WindowsNotifiedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.WindowsNotifiedAtUtc, nowUtc));
    }

    public async Task MarkPushNotifiedAsync(string id, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id && x.PushNotifiedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PushNotifiedAtUtc, nowUtc));
    }

    public async Task MarkSentAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id && x.State == ReminderState.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, ReminderState.Sent));
    }

    public async Task MarkMissedAsync(string id, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id && x.State == ReminderState.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, ReminderState.Missed)
                .SetProperty(x => x.ActionAtUtc, nowUtc));
    }

    public async Task RequeueForRepeatAsync(string id, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, ReminderState.Pending)
                .SetProperty(x => x.DueAtUtc, nowUtc)
                .SetProperty(x => x.WindowsNotifiedAtUtc, (DateTime?)null)
                .SetProperty(x => x.PushNotifiedAtUtc, (DateTime?)null)
                .SetProperty(x => x.RepeatCount, x => x.RepeatCount + 1));
    }

    public async Task RequeueForSnoozeAsync(string id, DateTime dueAtUtc, DateTime nowUtc, string? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, ReminderState.Pending)
                .SetProperty(x => x.DueAtUtc, dueAtUtc)
                .SetProperty(x => x.WindowsNotifiedAtUtc, (DateTime?)null)
                .SetProperty(x => x.PushNotifiedAtUtc, (DateTime?)null)
                .SetProperty(x => x.LastAction, ReminderAction.UnreachableSnooze)
                .SetProperty(x => x.ActionAtUtc, nowUtc)
                .SetProperty(x => x.ActionByUserId, userId)
                .SetProperty(x => x.SnoozeCount, x => x.SnoozeCount + 1));
    }

    public async Task MarkActionAsync(
        string id,
        ReminderState state,
        ReminderAction action,
        DateTime nowUtc,
        string? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ReminderDispatches
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, state)
                .SetProperty(x => x.LastAction, action)
                .SetProperty(x => x.ActionAtUtc, nowUtc)
                .SetProperty(x => x.ActionByUserId, userId));
    }

    public async Task MarkUnansweredAsync(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
            return;

        await using var db = await _factory.CreateDbContextAsync();
        foreach (var chunk in ids.Chunk(400))
        {
            var batch = chunk.ToArray();
            await db.ReminderDispatches
                .Where(x => batch.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.State, ReminderState.Unanswered));
        }
    }

    public async Task<IReadOnlyList<Appointment>> GetDeletedAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser)
            .Include(x => x.Category)
            .Where(x => x.IsDeleted)
            .OrderByDescending(x => x.DeletedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetHostUsageCountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var primary = await db.Appointments.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.HostUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var participants = await db.AppointmentHosts.AsNoTracking()
            .Where(x => !x.Appointment!.IsDeleted && !x.IsPrimary)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in primary)
            result[row.UserId] = result.GetValueOrDefault(row.UserId) + row.Count;

        foreach (var row in participants)
            result[row.UserId] = result.GetValueOrDefault(row.UserId) + row.Count;

        return result;
    }

    public async Task AddAsync(Appointment appointment)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IReadOnlyList<Appointment> appointments)
    {
        if (appointments.Count == 0)
            return;

        await using var db = await _factory.CreateDbContextAsync();
        db.Appointments.AddRange(appointments);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Appointment>> GetSeriesAsync(string seriesId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser).ThenInclude(x => x!.Title)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Participants).ThenInclude(x => x.User).ThenInclude(x => x!.Title)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .Where(x => x.SeriesId == seriesId)
            .OrderBy(x => x.StartsAtUtc)
            .ToListAsync();
    }

    public async Task<int> UpdateSeriesCountAsync(string seriesId, int count)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Appointments
            .Where(x => x.SeriesId == seriesId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SeriesCount, count));
    }

    public async Task SetSeriesMetadataAsync(string seriesId, SeriesFrequency frequency, DateTime anchorLocal)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.Appointments
            .Where(x => x.SeriesId == seriesId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.SeriesFrequency, frequency)
                .SetProperty(x => x.SeriesAnchorLocal, anchorLocal));
    }

    public async Task SaveAsync(
        Appointment appointment,
        IReadOnlyList<ReminderDispatch>? newReminders,
        bool cancelPendingReminders)
    {
        await using var db = await _factory.CreateDbContextAsync();

        if (cancelPendingReminders)
        {
            await db.ReminderDispatches
                .Where(x => x.AppointmentId == appointment.Id && x.State == ReminderState.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.State, ReminderState.Cancelled));
        }

        // AsNoTracking ile gelen eski navigation'lar, FK değişikliğini geri ezebilir.
        var participants = appointment.Participants.ToList();
        var guests = appointment.Guests.ToList();

        appointment.Person = null;
        appointment.HostUser = null;
        appointment.Room = null;
        appointment.Category = null;
        appointment.Outcome = null;
        appointment.Participants = new List<AppointmentHost>();
        appointment.Guests = new List<AppointmentGuest>();

        var attached = db.Appointments.Attach(appointment);
        attached.State = EntityState.Modified;

        if (newReminders is { Count: > 0 })
        {
            foreach (var reminder in newReminders)
                reminder.AppointmentId = appointment.Id;

            db.ReminderDispatches.AddRange(newReminders);
        }

        await SyncParticipantsAsync(db, appointment.Id, participants);
        await SyncGuestsAsync(db, appointment.Id, guests);

        await db.SaveChangesAsync();
        attached.State = EntityState.Detached;
    }

    public async Task SaveOutcomeAsync(
        Appointment appointment,
        AppointmentOutcome outcome,
        IReadOnlyList<AppointmentGuest>? guests,
        bool cancelPendingReminders)
    {
        await using var db = await _factory.CreateDbContextAsync();

        await using var transaction = await db.Database.BeginTransactionAsync();

        if (cancelPendingReminders)
        {
            await db.ReminderDispatches
                .Where(x => x.AppointmentId == appointment.Id && x.State == ReminderState.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.State, ReminderState.Cancelled));
        }

        appointment.Person = null;
        appointment.HostUser = null;
        appointment.Room = null;
        appointment.Category = null;
        appointment.Outcome = null;
        appointment.Participants = new List<AppointmentHost>();
        appointment.Guests = new List<AppointmentGuest>();

        var attachedAppointment = db.Appointments.Attach(appointment);
        attachedAppointment.State = EntityState.Modified;

        outcome.AppointmentId = appointment.Id;
        var exists = await db.AppointmentOutcomes
            .AsNoTracking()
            .AnyAsync(x => x.AppointmentId == appointment.Id);

        if (exists)
            db.AppointmentOutcomes.Update(outcome);
        else
            db.AppointmentOutcomes.Add(outcome);

        if (guests is { Count: > 0 })
            await SyncGuestsAsync(db, appointment.Id, guests);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        attachedAppointment.State = EntityState.Detached;
    }

    private static async Task SyncParticipantsAsync(
        AppDbContext db,
        string appointmentId,
        IReadOnlyList<AppointmentHost> participants)
    {
        await db.AppointmentHosts
            .Where(x => x.AppointmentId == appointmentId)
            .ExecuteDeleteAsync();

        foreach (var participant in participants
                     .GroupBy(x => x.UserId)
                     .Select(g => g.First()))
        {
            db.AppointmentHosts.Add(new AppointmentHost
            {
                AppointmentId = appointmentId,
                UserId = participant.UserId,
                IsPrimary = participant.IsPrimary,
                SortOrder = participant.SortOrder,
                CreatedAtUtc = participant.CreatedAtUtc
            });
        }
    }

    private static async Task SyncGuestsAsync(
        AppDbContext db,
        string appointmentId,
        IReadOnlyList<AppointmentGuest> guests)
    {
        var incoming = guests
            .GroupBy(x => x.PersonId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var existing = await db.AppointmentGuests
            .Where(x => x.AppointmentId == appointmentId)
            .ToListAsync();

        foreach (var row in existing)
        {
            if (!incoming.TryGetValue(row.PersonId, out var updated))
            {
                db.AppointmentGuests.Remove(row);
                continue;
            }

            row.IsPrimary = updated.IsPrimary;
            row.SortOrder = updated.SortOrder;
            row.Attendance = updated.Attendance;
            row.Summary = updated.Summary;
            row.PostNotes = updated.PostNotes;
            row.WillRepeat = updated.WillRepeat;
            row.IsClosed = updated.IsClosed;
            row.ClosedAtUtc = updated.ClosedAtUtc;
            row.ClosedByUserId = updated.ClosedByUserId;
            row.UpdatedAtUtc = updated.UpdatedAtUtc;
            row.UpdatedByUserId = updated.UpdatedByUserId;

            incoming.Remove(row.PersonId);
        }

        foreach (var guest in incoming.Values)
        {
            db.AppointmentGuests.Add(new AppointmentGuest
            {
                Id = string.IsNullOrWhiteSpace(guest.Id)
                    ? Guid.NewGuid().ToString("N")
                    : guest.Id,
                AppointmentId = appointmentId,
                PersonId = guest.PersonId,
                IsPrimary = guest.IsPrimary,
                SortOrder = guest.SortOrder,
                Attendance = guest.Attendance,
                Summary = guest.Summary,
                PostNotes = guest.PostNotes,
                WillRepeat = guest.WillRepeat,
                IsClosed = guest.IsClosed,
                ClosedAtUtc = guest.ClosedAtUtc,
                ClosedByUserId = guest.ClosedByUserId,
                UpdatedAtUtc = guest.UpdatedAtUtc,
                UpdatedByUserId = guest.UpdatedByUserId,
                CreatedAtUtc = guest.CreatedAtUtc
            });
        }
    }

    public async Task SoftDeleteAsync(string id, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is null)
            return;

        appointment.IsDeleted = true;
        appointment.DeletedAtUtc = nowUtc;
        appointment.UpdatedAtUtc = nowUtc;
        appointment.RowVersion++;

        await db.ReminderDispatches
            .Where(x => x.AppointmentId == id && x.State == ReminderState.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, ReminderState.Cancelled));

        await db.SaveChangesAsync();
    }

    public async Task<int> AnonymizeBeforeAsync(DateTime cutoffUtc, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var appointments = await db.Appointments
            .Include(x => x.Person)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .Where(x => x.StartsAtUtc < cutoffUtc)
            .ToListAsync();

        if (appointments.Count == 0)
            return 0;

        var personIds = appointments
            .SelectMany(x => x.Guests.Select(g => g.PersonId).Append(x.PersonId))
            .Distinct()
            .ToList();

        var recentAppointments = await db.Appointments.AsNoTracking()
            .Include(x => x.Guests)
            .Where(x => x.StartsAtUtc >= cutoffUtc
                        && (personIds.Contains(x.PersonId)
                            || x.Guests.Any(g => personIds.Contains(g.PersonId))))
            .ToListAsync();

        var recent = recentAppointments
            .SelectMany(x => x.Guests.Select(g => g.PersonId).Append(x.PersonId))
            .ToHashSet(StringComparer.Ordinal);
        var changed = 0;

        foreach (var appointment in appointments)
        {
            var appointmentChanged = false;
            if (!string.IsNullOrWhiteSpace(appointment.Description))
            {
                appointment.Description = null;
                appointmentChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(appointment.StudentFirstName))
            {
                appointment.StudentFirstName = null;
                appointmentChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(appointment.StudentLastName))
            {
                appointment.StudentLastName = null;
                appointmentChanged = true;
            }

            var people = appointment.Guests
                .Select(x => x.Person)
                .Append(appointment.Person)
                .Where(x => x is not null)
                .Select(x => x!)
                .GroupBy(x => x.Id)
                .Select(g => g.First());

            foreach (var person in people)
            {
                if (recent.Contains(person.Id) || person.FirstName == "Anonim")
                    continue;

                person.FirstName = "Anonim";
                person.LastName = "Kişi";
                person.PhoneE164 = "+900000000000";
                person.Email = null;
                person.Organization = null;
                person.Notes = null;
                appointmentChanged = true;
            }

            if (!appointmentChanged)
                continue;

            appointment.UpdatedAtUtc = nowUtc;
            appointment.UpdatedByUserId = null;
            appointment.RowVersion++;
            changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync();

        return changed;
    }

    public async Task<bool> RestoreAsync(string id, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var appointment = await db.Appointments
            .Include(x => x.Reminders)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment is null || !appointment.IsDeleted)
            return false;

        appointment.IsDeleted = false;
        appointment.DeletedAtUtc = null;
        appointment.UpdatedAtUtc = nowUtc;
        appointment.RowVersion++;

        // Silme sırasında iptal edilen hatırlatıcılar, randevu hâlâ gelecekteyse
        // yeniden etkinleştirilir. Aynı aralık için eski nesil kayıtlar varsa yalnızca
        // en son kayıt canlandırılır; böylece mükerrer hatırlatıcı oluşmaz.
        if (appointment.IsActiveForReminders && appointment.StartsAtUtc > nowUtc)
        {
            var cancelledByOffset = appointment.Reminders
                .Where(x => x.State == ReminderState.Cancelled)
                .GroupBy(x => new { x.Kind, x.OffsetMinutes });

            foreach (var group in cancelledByOffset)
            {
                var alreadyActive = appointment.Reminders.Any(x =>
                    x.Kind == group.Key.Kind
                    && x.OffsetMinutes == group.Key.OffsetMinutes
                    && x.State != ReminderState.Cancelled);

                if (alreadyActive)
                    continue;

                var latest = group
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .First();

                latest.State = ReminderState.Pending;
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<IReadOnlyList<string>> PurgeDeletedBeforeAsync(DateTime cutoffUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var ids = await db.Appointments.AsNoTracking()
            .Where(x => x.IsDeleted
                        && x.DeletedAtUtc != null
                        && x.DeletedAtUtc < cutoffUtc)
            .Select(x => x.Id)
            .ToListAsync();

        if (ids.Count == 0)
            return ids;

        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var chunk in ids.Chunk(400))
        {
            var batch = chunk.ToArray();
            await db.Appointments
                .Where(x => batch.Contains(x.Id))
                .ExecuteDeleteAsync();
        }

        await transaction.CommitAsync();

        return ids;
    }

    private static bool MatchesText(Appointment appointment, string term, string digits)
    {
        var people = appointment.Guests
            .Select(x => x.Person)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        if (appointment.Person is not null
            && people.All(x => x.Id != appointment.Person.Id))
        {
            people.Insert(0, appointment.Person);
        }

        if (people.Any(person => Contains(person.FirstName, term)
                                 || Contains(person.LastName, term)
                                 || Contains(person.FullName, term)))
        {
            return true;
        }

        if (Contains(appointment.Category?.Name, term)
            || Contains(appointment.HostUser?.DisplayName, term)
            || appointment.Participants.Any(x => Contains(x.User?.DisplayName, term)))
        {
            return true;
        }

        return digits.Length >= 3
               && people.Any(person => person.PhoneE164.Contains(digits, StringComparison.Ordinal));
    }

    private static bool Contains(string? value, string term)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
}
