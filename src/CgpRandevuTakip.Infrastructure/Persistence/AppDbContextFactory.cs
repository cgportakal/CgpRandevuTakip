using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Persistence;

public static class AppDbContextFactory
{
    public static AppDbContext Create(string? databasePath = null)
    {
        var path = databasePath ?? AppPaths.DatabasePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .EnableSensitiveDataLogging(false)
            .Options;

        return new AppDbContext(options);
    }
}
