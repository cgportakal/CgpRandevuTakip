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
            .Include(x => x.HostUser)
            .Include(x => x.Category)
            .Include(x => x.Outcome)
            .FirstOrDefaultAsync(x => x.Id == id);
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
            .Include(x => x.HostUser)
            .Include(x => x.Category)
            .Where(x => !x.IsDeleted
                        && !x.IsClosed
                        && x.StartsAtUtc >= nowUtc
                        && (x.Status == AppointmentStatus.Scheduled
                            || x.Status == AppointmentStatus.Arrived));

        if (!string.IsNullOrWhiteSpace(hostUserId))
            query = query.Where(x => x.HostUserId == hostUserId);

        return await query
            .OrderBy(x => x.StartsAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Appointment>> QueryAsync(AppointmentQuery query)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var rows = db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser)
            .Include(x => x.Category)
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
            rows = rows.Where(x => x.HostUserId == query.HostUserId);

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
        string hostUserId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? excludeAppointmentId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var windowStart = startsAtUtc.AddMinutes(-MaxDurationMinutes);

        var rows = await db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.Category)
            .Where(x => !x.IsDeleted
                        && x.HostUserId == hostUserId
                        && x.StartsAtUtc > windowStart
                        && x.StartsAtUtc < endsAtUtc
                        && (excludeAppointmentId == null || x.Id != excludeAppointmentId)
                        && (x.Status == AppointmentStatus.Scheduled
                            || x.Status == AppointmentStatus.Arrived
                            || x.Status == AppointmentStatus.Postponed))
            .ToListAsync();

        return rows
            .Where(x => x.StartsAtUtc < endsAtUtc
                        && x.StartsAtUtc.AddMinutes(x.DurationMinutes) > startsAtUtc)
            .Select(x => new AppointmentConflictRecord(
                x.Id,
                x.Person?.FirstName ?? string.Empty,
                x.Person?.LastName ?? string.Empty,
                x.Person?.PhoneE164 ?? string.Empty,
                x.StartsAtUtc,
                x.StartsAtUtc.AddMinutes(x.DurationMinutes),
                x.Category?.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<ReminderDispatch>> GetRemindersAsync(string appointmentId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ReminderDispatches.AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.DueAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetHostUsageCountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Appointments.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.HostUserId)
            .Select(g => new { HostUserId = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(x => x.HostUserId, x => x.Count);
    }

    public async Task AddAsync(Appointment appointment)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
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

        var attached = db.Appointments.Attach(appointment);
        attached.State = EntityState.Modified;

        if (newReminders is { Count: > 0 })
        {
            foreach (var reminder in newReminders)
                reminder.AppointmentId = appointment.Id;

            db.ReminderDispatches.AddRange(newReminders);
        }

        await db.SaveChangesAsync();
        attached.State = EntityState.Detached;
    }

    public async Task SaveOutcomeAsync(
        Appointment appointment,
        AppointmentOutcome outcome,
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

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        attachedAppointment.State = EntityState.Detached;
    }

    public async Task SoftDeleteAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is null)
            return;

        var now = DateTime.UtcNow;
        appointment.IsDeleted = true;
        appointment.DeletedAtUtc = now;
        appointment.UpdatedAtUtc = now;
        appointment.RowVersion++;

        await db.ReminderDispatches
            .Where(x => x.AppointmentId == id && x.State == ReminderState.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, ReminderState.Cancelled));

        await db.SaveChangesAsync();
    }

    public async Task RestoreAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is null)
            return;

        appointment.IsDeleted = false;
        appointment.DeletedAtUtc = null;
        appointment.UpdatedAtUtc = DateTime.UtcNow;
        appointment.RowVersion++;
        await db.SaveChangesAsync();
    }

    private static bool MatchesText(Appointment appointment, string term, string digits)
    {
        var person = appointment.Person;
        if (person is null)
            return false;

        if (Contains(person.FirstName, term)
            || Contains(person.LastName, term)
            || Contains(person.FullName, term)
            || Contains(appointment.Category?.Name, term)
            || Contains(appointment.HostUser?.DisplayName, term))
        {
            return true;
        }

        return digits.Length >= 3 && person.PhoneE164.Contains(digits, StringComparison.Ordinal);
    }

    private static bool Contains(string? value, string term)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
}
