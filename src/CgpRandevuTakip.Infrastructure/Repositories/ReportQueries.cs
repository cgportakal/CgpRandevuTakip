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
        string? hostUserId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Appointments.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.StartsAtUtc >= fromUtc
                        && x.StartsAtUtc < toUtc);

        if (!string.IsNullOrWhiteSpace(categoryId))
            query = query.Where(x => x.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(hostUserId))
            query = query.Where(x => x.HostUserId == hostUserId);

        return await query
            .Select(x => new ReportRow(
                x.Id,
                x.PersonId,
                x.HostUserId,
                x.HostUser != null ? x.HostUser.DisplayName : string.Empty,
                x.CategoryId,
                x.Category != null ? x.Category.Name : string.Empty,
                x.Category != null ? x.Category.ColorHex : "#5D7166",
                x.StartsAtUtc,
                x.DurationMinutes,
                x.Status,
                x.IsClosed,
                x.Outcome != null && x.Outcome.WillRepeat))
            .ToListAsync();
    }
}
