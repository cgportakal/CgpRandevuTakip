using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class PersonRepository : IPersonRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PersonRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Person?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Persons.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<Person?> FindByPhoneAndNameAsync(
        string phoneE164,
        string firstName,
        string lastName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var candidates = await db.Persons.AsNoTracking()
            .Where(x => x.PhoneE164 == phoneE164 && !x.IsDeleted)
            .ToListAsync();

        return candidates.FirstOrDefault(x =>
            string.Equals(x.FirstName, firstName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.LastName, lastName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddAsync(Person person)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Persons.Add(person);
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(Person person)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Persons.Update(person);
        await db.SaveChangesAsync();
    }
}
