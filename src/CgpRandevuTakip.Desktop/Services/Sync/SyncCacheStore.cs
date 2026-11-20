using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>Sunucu yanıtlarının yerel önbelleği.</summary>
public sealed record SyncCacheValue(string Payload, string? ETag, DateTime UpdatedAtUtc);

public interface ISyncCacheStore
{
    Task<SyncCacheValue?> GetAsync(string key, CancellationToken ct = default);

    Task SaveAsync(string key, string payload, string? etag = null, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task ClearAsync(CancellationToken ct = default);
}

/// <summary>
/// Önbelleği yerel SQLite veritabanındaki <c>SyncCacheEntries</c> tablosunda tutar.
/// Yalnızca okuma yolunda kullanılır; yerel iş verisiyle karışmaz.
/// </summary>
public sealed class SyncCacheStore : ISyncCacheStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SyncCacheStore(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<SyncCacheValue?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entry = await db.SyncCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);

        return entry is null
            ? null
            : new SyncCacheValue(entry.Payload, entry.ETag, entry.UpdatedAtUtc);
    }

    public async Task SaveAsync(
        string key,
        string payload,
        string? etag = null,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entry = await db.SyncCacheEntries.FirstOrDefaultAsync(x => x.Key == key, ct);

        if (entry is null)
        {
            db.SyncCacheEntries.Add(new SyncCacheEntry
            {
                Key = key,
                Payload = payload,
                ETag = etag,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            entry.Payload = payload;
            entry.ETag = etag;
            entry.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.SyncCacheEntries
            .Where(x => x.Key == key)
            .ExecuteDeleteAsync(ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.SyncCacheEntries.ExecuteDeleteAsync(ct);
    }
}
