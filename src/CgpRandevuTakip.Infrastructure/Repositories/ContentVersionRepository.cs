using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class ContentVersionRepository : IContentVersionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ContentVersionRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<ContentVersion?> GetLatestAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ContentVersions.AsNoTracking()
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ContentVersion>> GetRecentAsync(int count)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ContentVersions.AsNoTracking()
            .OrderByDescending(x => x.VersionNumber)
            .Take(count)
            .ToListAsync();
    }

    public async Task<ContentVersion?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ContentVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> GetNextVersionNumberAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ContentVersions.AsNoTracking()
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync() is { } max
            ? max + 1
            : 1;
    }

    public async Task AddAsync(ContentVersion version)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.ContentVersions.Add(version);
        await db.SaveChangesAsync();
    }
}
