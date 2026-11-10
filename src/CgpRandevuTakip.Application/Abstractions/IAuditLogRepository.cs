using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

/// <summary>Denetim kaydı sorgusu. Tarihler UTC'dir.</summary>
public sealed record AuditLogQuery(
    string? UserId,
    AuditAction? Action,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Skip,
    int Take);

public sealed record AuditLogRecord(
    string Id,
    DateTime CreatedAtUtc,
    string? UserId,
    string? UserName,
    AuditAction Action,
    string? EntityType,
    string? EntityId,
    string? DetailsJson);

public interface IAuditLogRepository
{
    Task<int> CountAsync(AuditLogQuery query);
    Task<IReadOnlyList<AuditLogRecord>> QueryAsync(AuditLogQuery query);
}
