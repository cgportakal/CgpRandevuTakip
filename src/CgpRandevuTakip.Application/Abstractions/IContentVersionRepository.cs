using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IContentVersionRepository
{
    Task<ContentVersion?> GetLatestAsync();
    Task<IReadOnlyList<ContentVersion>> GetRecentAsync(int count);
    Task<ContentVersion?> GetByIdAsync(string id);
    Task<int> GetNextVersionNumberAsync();
    Task AddAsync(ContentVersion version);
}
