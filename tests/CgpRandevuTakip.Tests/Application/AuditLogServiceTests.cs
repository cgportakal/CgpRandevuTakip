using CgpRandevuTakip.Application.AuditLogs;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Auditing;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class AuditLogServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required AuditLogService Service { get; init; }
        public required string AdminId { get; init; }
        public required string OtherId { get; init; }

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();
        var clock = new FixedClock(NowUtc);
        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("admin-1", "admin", "Yönetici", UserRole.Admin));

        var admin = new User
        {
            Id = "admin-1",
            Username = "admin",
            DisplayName = "Yönetici Kullanıcı",
            Role = UserRole.Admin,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        var other = new User
        {
            Id = "user-2",
            Username = "sekreter",
            DisplayName = "Sekreter Kullanıcı",
            Role = UserRole.Secretary,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        await using (var context = db.CreateContext())
        {
            context.Users.AddRange(admin, other);
            context.AuditLogs.AddRange(
                new AuditLog
                {
                    CreatedAtUtc = NowUtc.AddHours(-1),
                    UserId = admin.Id,
                    Action = AuditAction.AppointmentCreated,
                    EntityType = "Appointment",
                    EntityId = "appt-1",
                    DetailsJson = "{\"test\":1}"
                },
                new AuditLog
                {
                    CreatedAtUtc = NowUtc.AddHours(-2),
                    UserId = other.Id,
                    Action = AuditAction.BackupCreated,
                    EntityType = "BackupRecord",
                    EntityId = "backup-1"
                },
                new AuditLog
                {
                    CreatedAtUtc = NowUtc.AddDays(-2),
                    Action = AuditAction.LoginSucceeded
                });

            await context.SaveChangesAsync();
        }

        var service = new AuditLogService(
            new AuditLogRepository(db.Factory),
            clock,
            currentUser);

        return new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Service = service,
            AdminId = admin.Id,
            OtherId = other.Id
        };
    }

    [Fact]
    public async Task Query_ReturnsNewestFirstWithUserNames()
    {
        using var sut = await CreateAsync();

        var page = await sut.Service.QueryAsync(new AuditLogFilter(null, null, null, null));

        Assert.Null(page.Error);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal("Yönetici Kullanıcı", page.Items[0].UserName);
        Assert.Equal("Görüşme eklendi", page.Items[0].ActionText);
        Assert.Equal("Sekreter Kullanıcı", page.Items[1].UserName);
        Assert.Equal("Sistem", page.Items[2].UserName);
    }

    [Fact]
    public async Task Query_FiltersByActionAndUser()
    {
        using var sut = await CreateAsync();

        var backupPage = await sut.Service.QueryAsync(
            new AuditLogFilter(null, AuditAction.BackupCreated, null, null));
        Assert.Single(backupPage.Items);
        Assert.Equal("Sekreter Kullanıcı", backupPage.Items[0].UserName);

        var userPage = await sut.Service.QueryAsync(
            new AuditLogFilter(sut.AdminId, null, null, null));
        Assert.Equal(1, userPage.TotalCount);
        Assert.Equal("Yönetici Kullanıcı", userPage.Items[0].UserName);
    }

    [Fact]
    public async Task Query_FiltersByLocalDateRange()
    {
        using var sut = await CreateAsync();

        // Yerel saat UTC+3: 2 gün önceki kayıt 2026-11-04 yereline düşer.
        var page = await sut.Service.QueryAsync(new AuditLogFilter(
            null,
            null,
            new DateTime(2026, 11, 5),
            new DateTime(2026, 11, 6)));

        Assert.Equal(2, page.TotalCount);
        Assert.DoesNotContain(page.Items, x => x.Action == AuditAction.LoginSucceeded);
    }

    [Fact]
    public async Task Query_RequiresAdmin()
    {
        using var sut = await CreateAsync();
        sut.CurrentUser.SignIn(new UserSession("user-2", "sekreter", "Sekreter", UserRole.Secretary));

        var page = await sut.Service.QueryAsync(new AuditLogFilter(null, null, null, null));

        Assert.NotNull(page.Error);
        Assert.Contains("yönetici", page.Error);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Query_RejectsReversedDateRange()
    {
        using var sut = await CreateAsync();

        var page = await sut.Service.QueryAsync(new AuditLogFilter(
            null,
            null,
            new DateTime(2026, 11, 6),
            new DateTime(2026, 11, 5)));

        Assert.NotNull(page.Error);
        Assert.Contains("Başlangıç", page.Error);
    }
}
