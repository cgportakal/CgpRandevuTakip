using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync();
    Task<IReadOnlyList<Category>> GetActiveAsync();
    Task<Category?> GetByIdAsync(string id);
    Task<bool> NameExistsAsync(string name, string? excludeId = null);
    Task<IReadOnlyDictionary<string, int>> GetUsageCountsAsync();
    Task<int> GetMaxSortOrderAsync();
    Task AddAsync(Category category);
    Task SaveAsync(Category category);
    Task SaveRangeAsync(IEnumerable<Category> categories);
    Task DeleteAsync(Category category);
}
