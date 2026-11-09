using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class RecipientRepository : IRecipientRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public RecipientRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Recipient>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Recipients.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Recipient?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Recipients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(Recipient recipient)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Recipients.Add(recipient);
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(Recipient recipient)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Recipients.Update(recipient);
        await db.SaveChangesAsync();
    }
}
