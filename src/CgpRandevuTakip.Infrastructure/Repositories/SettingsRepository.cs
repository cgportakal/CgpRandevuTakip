using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SettingsRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Settings.AsNoTracking().ToListAsync();
        return rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }

    public async Task SetManyAsync(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
            return;

        await using var db = await _factory.CreateDbContextAsync();
        var keys = values.Keys.ToList();
        var existing = await db.Settings
            .Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var setting))
            {
                setting.Value = value;
                setting.UpdatedAtUtc = now;
            }
            else
            {
                db.Settings.Add(new Setting
                {
                    Key = key,
                    Value = value,
                    UpdatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
