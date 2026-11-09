using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IShareLogRepository
{
    Task AddAsync(WhatsAppShareLog log);
    Task<IReadOnlyList<WhatsAppShareLog>> GetRecentAsync(int count);
}
