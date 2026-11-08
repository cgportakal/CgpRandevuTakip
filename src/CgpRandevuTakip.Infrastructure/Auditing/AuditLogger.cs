using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Auditing;

public sealed class AuditLogger : IAuditLogger
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IClock _clock;

    public AuditLogger(IDbContextFactory<AppDbContext> factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task LogAsync(
        AuditAction action,
        string? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? detailsJson = null)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            db.AuditLogs.Add(new AuditLog
            {
                CreatedAtUtc = _clock.UtcNow,
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                DetailsJson = detailsJson
            });
            await db.SaveChangesAsync();
        }
        catch
        {
            // Denetim kaydı iş akışını durdurmamalıdır; hata üst katmanda günlüğe yazılır.
        }
    }
}
