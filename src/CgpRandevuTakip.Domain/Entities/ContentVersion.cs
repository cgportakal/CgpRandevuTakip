using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>Yayınlanan içerik anlık görüntüsü. Geri alma için kullanılır.</summary>
public class ContentVersion : EntityBase
{
    public int VersionNumber { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public string? PublishedByUserId { get; set; }
    public string? PublishedByUserName { get; set; }
    public string? Note { get; set; }
    public string SnapshotJson { get; set; } = "{}";
}
