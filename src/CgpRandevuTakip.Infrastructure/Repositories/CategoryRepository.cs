using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CategoryRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Category>> GetActiveAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<bool> NameExistsAsync(string name, string? excludeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking()
            .AnyAsync(x => !x.IsDeleted
                           && x.Name == name
                           && (excludeId == null || x.Id != excludeId));
    }

    public async Task<IReadOnlyDictionary<string, int>> GetUsageCountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Appointments.AsNoTracking()
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(x => x.CategoryId, x => x.Count);
    }

    public async Task<int> GetMaxSortOrderAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? -1;
    }

    public async Task AddAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Categories.Add(category);
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Categories.Update(category);
        await db.SaveChangesAsync();
    }

    public async Task SaveRangeAsync(IEnumerable<Category> categories)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Categories.UpdateRange(categories);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Categories.Remove(category);
        await db.SaveChangesAsync();
    }
}
