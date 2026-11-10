using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Auditing;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AuditLogRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> CountAsync(AuditLogQuery query)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await BuildQuery(db, query).CountAsync();
    }

    public async Task<IReadOnlyList<AuditLogRecord>> QueryAsync(AuditLogQuery query)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var rows = await BuildQuery(db, query)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 500))
            .Select(x => new
            {
                x.Id,
                x.CreatedAtUtc,
                x.UserId,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.DetailsJson
            })
            .ToListAsync();

        var userIds = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.UserId))
            .Select(x => x.UserId!)
            .Distinct()
            .ToList();

        var names = userIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName);

        return rows.Select(x => new AuditLogRecord(
                x.Id,
                x.CreatedAtUtc,
                x.UserId,
                x.UserId is not null && names.TryGetValue(x.UserId, out var name) ? name : null,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.DetailsJson))
            .ToList();
    }

    private static IQueryable<AuditLog> BuildQuery(AppDbContext db, AuditLogQuery query)
    {
        var rows = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.UserId))
            rows = rows.Where(x => x.UserId == query.UserId);

        if (query.Action is not null)
            rows = rows.Where(x => x.Action == query.Action.Value);

        if (query.FromUtc is not null)
            rows = rows.Where(x => x.CreatedAtUtc >= query.FromUtc.Value);

        if (query.ToUtc is not null)
            rows = rows.Where(x => x.CreatedAtUtc <= query.ToUtc.Value);

        return rows;
    }
}
