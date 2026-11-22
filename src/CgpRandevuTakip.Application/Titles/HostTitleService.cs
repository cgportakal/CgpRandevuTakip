using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Titles;

public sealed class HostTitleService : IHostTitleService
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 120;

    private readonly IHostTitleRepository _titles;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;

    public HostTitleService(
        IHostTitleRepository titles,
        IAuditLogger audit,
        ICurrentUser currentUser)
    {
        _titles = titles;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<HostTitleDto>> GetAllAsync()
    {
        var entities = await _titles.GetAllAsync();
        var usage = await _titles.GetUsageCountsAsync();
        return entities.Select(x => ToDto(x, usage)).ToList();
    }

    public async Task<IReadOnlyList<HostTitleDto>> GetActiveAsync()
    {
        var entities = await _titles.GetActiveAsync();
        var usage = await _titles.GetUsageCountsAsync();
        return entities.Select(x => ToDto(x, usage)).ToList();
    }

    public async Task<OperationResult> CreateAsync(HostTitleInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Ünvan ekleme yetkisi yalnızca yöneticidedir.");

        var name = Normalize(input.Name);
        var validation = Validate(name);
        if (validation is not null)
            return OperationResult.Fail(validation);

        if (await _titles.NameExistsAsync(name))
            return OperationResult.Fail("Bu adda bir ünvan zaten var.");

        var title = new HostTitle
        {
            Name = name,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive
        };

        await _titles.AddAsync(title);
        await _audit.LogAsync(
            AuditAction.HostTitleCreated,
            _currentUser.UserId,
            nameof(HostTitle),
            title.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(string id, HostTitleInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Ünvan düzenleme yetkisi yalnızca yöneticidedir.");

        var title = await _titles.GetByIdAsync(id);
        if (title is null)
            return OperationResult.Fail("Ünvan bulunamadı.");

        var name = Normalize(input.Name);
        var validation = Validate(name);
        if (validation is not null)
            return OperationResult.Fail(validation);

        if (await _titles.NameExistsAsync(name, id))
            return OperationResult.Fail("Bu adda bir ünvan zaten var.");

        title.Name = name;
        title.SortOrder = input.SortOrder;
        title.IsActive = input.IsActive;
        await _titles.SaveAsync(title);

        await _audit.LogAsync(
            input.IsActive ? AuditAction.HostTitleUpdated : AuditAction.HostTitleDeactivated,
            _currentUser.UserId,
            nameof(HostTitle),
            title.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetActiveAsync(string id, bool isActive)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Ünvan durumunu yalnızca yönetici değiştirebilir.");

        var title = await _titles.GetByIdAsync(id);
        if (title is null)
            return OperationResult.Fail("Ünvan bulunamadı.");

        if (title.IsActive == isActive)
            return OperationResult.Ok();

        title.IsActive = isActive;
        await _titles.SaveAsync(title);

        await _audit.LogAsync(
            isActive ? AuditAction.HostTitleUpdated : AuditAction.HostTitleDeactivated,
            _currentUser.UserId,
            nameof(HostTitle),
            title.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(string id)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Ünvan silme yetkisi yalnızca yöneticidedir.");

        var title = await _titles.GetByIdAsync(id);
        if (title is null)
            return OperationResult.Fail("Ünvan bulunamadı.");

        var usage = await _titles.GetUsageCountsAsync();
        if (usage.TryGetValue(id, out var count) && count > 0)
            return OperationResult.Fail(
                $"Bu ünvan {count} görüşmeciye atanmış. Önce görüşmecilerden kaldırın veya pasifleştirin.");

        await _titles.DeleteAsync(title);
        await _audit.LogAsync(
            AuditAction.HostTitleDeactivated,
            _currentUser.UserId,
            nameof(HostTitle),
            title.Id);

        return OperationResult.Ok();
    }

    private bool IsAdmin() => _currentUser.IsInRole(UserRole.Admin);

    private static HostTitleDto ToDto(HostTitle title, IReadOnlyDictionary<string, int> usage)
        => new(
            title.Id,
            title.Name,
            title.SortOrder,
            title.IsActive,
            usage.TryGetValue(title.Id, out var count) ? count : 0);

    private static string Normalize(string? name) => name?.Trim() ?? string.Empty;

    private static string? Validate(string name)
    {
        if (name.Length < MinNameLength)
            return $"Ünvan en az {MinNameLength} karakter olmalıdır.";

        return name.Length > MaxNameLength
            ? $"Ünvan en fazla {MaxNameLength} karakter olabilir."
            : null;
    }
}
