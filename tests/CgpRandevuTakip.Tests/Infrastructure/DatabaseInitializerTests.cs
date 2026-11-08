using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CgpRandevuTakip.Tests.Infrastructure;

public class DatabaseInitializerTests
{
    private static (SqliteConnection Connection, AppDbContext Db) CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return (connection, new AppDbContext(options));
    }

    [Fact]
    public void Initialize_CreatesSchemaAndSeedsCategories()
    {
        var (connection, db) = CreateInMemoryContext();
        using (connection)
        using (db)
        {
            var summary = DatabaseInitializer.Initialize(db, ":memory:");

            Assert.Equal(7, summary.CategoryCount);
            Assert.Equal(0, summary.AppointmentCount);
            Assert.Equal(0, summary.UserCount);
            Assert.Equal(7, db.Categories.Count());
        }
    }

    [Fact]
    public void Initialize_RunTwice_DoesNotDuplicateCategories()
    {
        var (connection, db) = CreateInMemoryContext();
        using (connection)
        using (db)
        {
            DatabaseInitializer.Initialize(db, ":memory:");
            var summary = DatabaseInitializer.Initialize(db, ":memory:");

            Assert.Equal(7, summary.CategoryCount);
            Assert.Equal(7, db.Categories.Select(c => c.Name).Distinct().Count());
        }
    }

    [Fact]
    public void Initialize_SeedsEducationCategories()
    {
        var (connection, db) = CreateInMemoryContext();
        using (connection)
        using (db)
        {
            DatabaseInitializer.Initialize(db, ":memory:");

            var names = db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToList();

            Assert.Contains("Veli Görüşmesi", names);
            Assert.Contains("Öğrenci Görüşmesi", names);
            Assert.Contains("Rehberlik/Danışmanlık", names);
        }
    }
}
