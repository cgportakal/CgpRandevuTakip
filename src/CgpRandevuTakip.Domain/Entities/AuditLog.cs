using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public AuditAction Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
}
