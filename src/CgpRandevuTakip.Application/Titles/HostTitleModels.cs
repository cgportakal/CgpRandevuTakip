using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Titles;

public sealed record HostTitleDto(
    string Id,
    string Name,
    int SortOrder,
    bool IsActive,
    int HostCount);

public sealed record HostTitleInput(
    string Name,
    int SortOrder,
    bool IsActive);

public interface IHostTitleService
{
    Task<IReadOnlyList<HostTitleDto>> GetAllAsync();
    Task<IReadOnlyList<HostTitleDto>> GetActiveAsync();
    Task<OperationResult> CreateAsync(HostTitleInput input);
    Task<OperationResult> UpdateAsync(string id, HostTitleInput input);
    Task<OperationResult> SetActiveAsync(string id, bool isActive);
    Task<OperationResult> DeleteAsync(string id);
}
