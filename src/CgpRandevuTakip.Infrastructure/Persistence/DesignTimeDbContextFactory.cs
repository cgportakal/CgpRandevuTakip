using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CgpRandevuTakip.Infrastructure.Persistence;

/// <summary>Yalnızca <c>dotnet ef</c> komutları için kullanılır; uygulama çalışma zamanında kullanmaz.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new AppDbContext(options);
    }
}
