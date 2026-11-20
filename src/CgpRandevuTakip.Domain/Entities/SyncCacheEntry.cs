namespace CgpRandevuTakip.Domain.Entities;

/// <summary>
/// Sunucudan okunan yanıtların çevrimdışı önbelleği. Yalnızca okuma yolunda
/// kullanılır; yerel iş kuralları bu tabloyu değiştirmez.
/// </summary>
public sealed class SyncCacheEntry
{
    public string Key { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string? ETag { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
