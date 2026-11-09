using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IRecipientRepository
{
    Task<IReadOnlyList<Recipient>> GetAllAsync();
    Task<Recipient?> GetByIdAsync(string id);
    Task AddAsync(Recipient recipient);
    Task SaveAsync(Recipient recipient);
}
