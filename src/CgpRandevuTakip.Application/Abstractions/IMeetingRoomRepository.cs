using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IMeetingRoomRepository
{
    Task<IReadOnlyList<MeetingRoom>> GetAllAsync();
    Task<IReadOnlyList<MeetingRoom>> GetActiveAsync(string? hostUserId = null);
    Task<MeetingRoom?> GetByIdAsync(string id);
    Task<MeetingRoom?> GetHostRoomAsync(string hostUserId);
    Task<bool> NameExistsAsync(string name, string? excludeId = null);
    Task<IReadOnlyDictionary<string, int>> GetUsageCountsAsync();
    Task<int> CountUpcomingAsync(string roomId, DateTime nowUtc);
    Task<int> GetMaxSortOrderAsync(MeetingRoomKind kind);
    Task AddAsync(MeetingRoom room);
    Task SaveAsync(MeetingRoom room);
    Task DeleteAsync(MeetingRoom room);
}
