using CgpRandevuTakip.Domain.Entities;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(string id);
    Task<Person?> FindByPhoneAndNameAsync(string phoneE164, string firstName, string lastName);
    Task AddAsync(Person person);
    Task SaveAsync(Person person);
}
