using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

/// <summary>Rapor satırlarını tek sorguda okur; soft-delete edilmiş kayıtlar hariçtir.</summary>
public sealed class ReportQueries : IReportQueries
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ReportQueries(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<ReportRow>> GetRowsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryId,
        string? hostUserId,
        string? roomId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Appointments.AsNoTracking()
            .Include(x => x.Person)
            .Include(x => x.HostUser).ThenInclude(x => x!.Title)
            .Include(x => x.Room)
            .Include(x => x.Category)
            .Include(x => x.Outcome)
            .Include(x => x.Participants).ThenInclude(x => x.User).ThenInclude(x => x!.Title)
            .Include(x => x.Guests).ThenInclude(x => x.Person)
            .Where(x => !x.IsDeleted
                        && x.StartsAtUtc >= fromUtc
                        && x.StartsAtUtc < toUtc);

        if (!string.IsNullOrWhiteSpace(categoryId))
            query = query.Where(x => x.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(hostUserId))
        {
            query = query.Where(x =>
                x.HostUserId == hostUserId
                || x.Participants.Any(p => p.UserId == hostUserId));
        }

        if (!string.IsNullOrWhiteSpace(roomId))
            query = query.Where(x => x.RoomId == roomId);

        var rows = await query.ToListAsync();

        return rows.Select(x => new ReportRow(
                x.Id,
                x.PersonId,
                x.Person is null ? string.Empty : $"{x.Person.FirstName} {x.Person.LastName}".Trim(),
                x.Person?.PhoneE164 ?? string.Empty,
                x.HostUserId,
                x.HostUser?.DisplayName ?? string.Empty,
                x.CategoryId,
                x.Category?.Name ?? string.Empty,
                x.Category?.ColorHex ?? "#5D7166",
                x.StartsAtUtc,
                x.DurationMinutes,
                x.Status,
                x.IsClosed,
                x.Outcome?.WillRepeat ?? false,
                x.RoomId,
                x.Room?.Name,
                x.HostUser?.Title?.Name,
                x.Participants
                    .OrderBy(p => p.SortOrder)
                    .Select(p => p.UserId)
                    .ToList(),
                x.Participants
                    .OrderBy(p => p.SortOrder)
                    .Select(p => p.User?.DisplayName ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList(),
                x.Guests
                    .OrderBy(g => g.SortOrder)
                    .Select(g => new ReportGuestRow(
                        g.PersonId,
                        g.Person is null
                            ? string.Empty
                            : $"{g.Person.FirstName} {g.Person.LastName}".Trim(),
                        g.Person?.PhoneE164 ?? string.Empty,
                        g.Attendance,
                        g.IsPrimary))
                    .ToList()))
            .ToList();
    }
}
