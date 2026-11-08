using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

public class BackupRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public BackupProvider Provider { get; set; } = BackupProvider.Local;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
