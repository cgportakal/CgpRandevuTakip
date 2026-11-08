using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Support;

internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Options = options;
        Factory = new TestDbContextFactory(options);

        using var db = new AppDbContext(options);
        db.Database.Migrate();
    }

    public DbContextOptions<AppDbContext> Options { get; }
    public IDbContextFactory<AppDbContext> Factory { get; }

    public AppDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}

internal sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; set; }

    public DateTime ToLocal(DateTime utc) => utc.AddHours(3);

    public DateTime ToUtc(DateTime local) => local.AddHours(-3);
}

internal sealed class RecordingAuditLogger : IAuditLogger
{
    public List<AuditAction> Actions { get; } = new();

    public Task LogAsync(
        AuditAction action,
        string? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? detailsJson = null)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}

internal sealed class TestAppEnvironment : IAppEnvironment
{
    public TestAppEnvironment(string? defaultBackupDirectory = null)
    {
        DefaultBackupDirectory = defaultBackupDirectory
            ?? Path.Combine(Path.GetTempPath(), "CgpRandevuTakipTests", Guid.NewGuid().ToString("N"));
    }

    public string RootDirectory => Path.Combine(Path.GetTempPath(), "CgpRandevuTakipTests");
    public string DatabasePath => Path.Combine(RootDirectory, "data.db");
    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
    public string DefaultBackupDirectory { get; }
}
