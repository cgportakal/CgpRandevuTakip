using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IUserRepository
{
    Task<int> CountAsync();
    Task<int> CountActiveAdminsAsync();
    Task<int> CountActiveLoginAdminsAsync();
    Task<IReadOnlyList<User>> GetAllAsync();
    Task<User?> FindByUsernameAsync(string username);
    Task<User?> GetByIdAsync(string id);
    Task AddAsync(User user);
    Task SaveAsync(User user);
}
