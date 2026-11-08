using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CgpRandevuTakip.Infrastructure.Persistence;

/// <summary>Her işlem için kısa ömürlü DbContext üretir. Repository'ler bu fabrikayı kullanır.</summary>
public sealed class SqliteDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly string _databasePath;

    public SqliteDbContextFactory(string? databasePath = null)
    {
        _databasePath = databasePath ?? AppPaths.DatabasePath;
    }

    public AppDbContext CreateDbContext() => AppDbContextFactory.Create(_databasePath);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
