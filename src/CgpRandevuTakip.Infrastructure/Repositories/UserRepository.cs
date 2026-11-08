using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UserRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> CountAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.CountAsync();
    }

    public async Task<int> CountActiveAdminsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.CountAsync(x => x.IsActive && x.Role == Domain.Enums.UserRole.Admin);
    }

    public async Task<int> CountActiveLoginAdminsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.CountAsync(x =>
            x.IsActive && x.IsLoginEnabled && x.Role == Domain.Enums.UserRole.Admin);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .ToListAsync();
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username);
    }

    public async Task<User?> FindByEmailAsync(string email)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email != null && x.Email == email);
    }

    public async Task<User?> FindByRememberTokenHashAsync(string tokenHash)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RememberTokenHash == tokenHash);
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(User user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(User user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }
}
