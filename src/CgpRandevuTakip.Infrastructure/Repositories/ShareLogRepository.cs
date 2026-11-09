using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class ShareLogRepository : IShareLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ShareLogRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task AddAsync(WhatsAppShareLog log)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.WhatsAppShareLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<WhatsAppShareLog>> GetRecentAsync(int count)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.WhatsAppShareLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(count)
            .ToListAsync();
    }
}
