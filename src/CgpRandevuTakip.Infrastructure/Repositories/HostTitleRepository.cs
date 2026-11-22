using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class HostTitleRepository : IHostTitleRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public HostTitleRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<HostTitle>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.HostTitles.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HostTitle>> GetActiveAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.HostTitles.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<HostTitle?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.HostTitles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<bool> NameExistsAsync(string name, string? excludeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.HostTitles.AsNoTracking()
            .AnyAsync(x => x.Name == name && (excludeId == null || x.Id != excludeId));
    }

    public async Task<IReadOnlyDictionary<string, int>> GetUsageCountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Users.AsNoTracking()
            .Where(x => x.TitleId != null)
            .GroupBy(x => x.TitleId!)
            .Select(g => new { TitleId = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(x => x.TitleId, x => x.Count);
    }

    public async Task<int> GetMaxSortOrderAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.HostTitles.AsNoTracking()
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? -1;
    }

    public async Task AddAsync(HostTitle title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.HostTitles.Add(title);
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(HostTitle title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.HostTitles.Update(title);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(HostTitle title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.HostTitles.Remove(title);
        await db.SaveChangesAsync();
    }
}
