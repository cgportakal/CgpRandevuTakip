using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IHostTitleRepository
{
    Task<IReadOnlyList<HostTitle>> GetAllAsync();
    Task<IReadOnlyList<HostTitle>> GetActiveAsync();
    Task<HostTitle?> GetByIdAsync(string id);
    Task<bool> NameExistsAsync(string name, string? excludeId = null);
    Task<IReadOnlyDictionary<string, int>> GetUsageCountsAsync();
    Task<int> GetMaxSortOrderAsync();
    Task AddAsync(HostTitle title);
    Task SaveAsync(HostTitle title);
    Task DeleteAsync(HostTitle title);
}
