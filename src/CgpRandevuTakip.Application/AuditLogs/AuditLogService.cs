using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.AuditLogs;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AuditLogService(
        IAuditLogRepository repository,
        IClock clock,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<AuditLogPageResult> QueryAsync(
        AuditLogFilter filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return AuditLogPageResult.Fail("Denetim kayıtlarını yalnızca yönetici görüntüleyebilir.");

        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 25, 200);

        DateTime? fromUtc = filter.FromLocal is null
            ? null
            : _clock.ToUtc(filter.FromLocal.Value.Date);

        DateTime? toUtc = filter.ToLocal is null
            ? null
            : _clock.ToUtc(filter.ToLocal.Value.Date.AddDays(1)).AddTicks(-1);

        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
            return AuditLogPageResult.Fail("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var query = new AuditLogQuery(
            filter.UserId,
            filter.Action,
            fromUtc,
            toUtc,
            (normalizedPage - 1) * normalizedSize,
            normalizedSize);

        var total = await _repository.CountAsync(query);
        var rows = await _repository.QueryAsync(query);

        var items = rows.Select(x => new AuditLogEntryDto(
                x.Id,
                _clock.ToLocal(x.CreatedAtUtc),
                string.IsNullOrWhiteSpace(x.UserName)
                    ? string.IsNullOrWhiteSpace(x.UserId) ? "Sistem" : "Bilinmeyen kullanıcı"
                    : x.UserName,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.DetailsJson))
            .ToList();

        return new AuditLogPageResult(items, total, normalizedPage, normalizedSize);
    }
}
