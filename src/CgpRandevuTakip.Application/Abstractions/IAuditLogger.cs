using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(
        AuditAction action,
        string? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? detailsJson = null);
}
