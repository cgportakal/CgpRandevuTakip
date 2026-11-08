using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Infrastructure.Security;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class UserServiceTests
{
    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required RecordingAuditLogger Audit { get; init; }
        public required UserService Service { get; init; }
        public required User Admin { get; init; }
        public required User Host { get; init; }

        public void SignInAs(User user)
            => CurrentUser.SignIn(new UserSession(user.Id, user.Username, user.DisplayName, user.Role));

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();
        var users = new UserRepository(db.Factory);

        var admin = new User
        {
            Username = "admin",
            DisplayName = "Sistem Yöneticisi",
            Role = UserRole.Admin,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        var host = new User
        {
            Username = "host1",
            DisplayName = "Host Bir",
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        await users.AddAsync(admin);
        await users.AddAsync(host);

        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession(admin.Id, admin.Username, admin.DisplayName, admin.Role));
        var audit = new RecordingAuditLogger();

        return new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Audit = audit,
            Service = new UserService(
                new UserRepository(db.Factory),
                new Pbkdf2PasswordHasher(),
                audit,
                new FixedClock(),
                currentUser),
            Admin = admin,
            Host = host
        };
    }

    [Fact]
    public async Task Create_NormalizesUsernameAndHashesPassword()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(new UserCreateInput(
            "  Ayse.Yilmaz  ",
            "Ayşe Yılmaz",
            UserRole.Secretary,
            true,
            "GizliSifre123",
            "GizliSifre123"));

        Assert.True(result.Success, result.Error);
        Assert.Contains(AuditAction.UserCreated, sut.Audit.Actions);

        using var db = sut.Db.CreateContext();
        var user = db.Users.Single(x => x.Username == "ayse.yilmaz");
        Assert.Equal(UserRole.Secretary, user.Role);
        Assert.NotEqual("GizliSifre123", user.PasswordHash);
        Assert.True(user.PasswordIterations > 1);

        var rows = await sut.Service.GetAllAsync();
        Assert.Contains(rows, x => x.Username == "ayse.yilmaz" && x.RoleText == "Sekreter");
    }

    [Fact]
    public async Task Create_RejectsDuplicateUsername()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(new UserCreateInput(
            "HOST1", "Başka Host", UserRole.Host, true, "GizliSifre123", "GizliSifre123"));

        Assert.False(result.Success);
        Assert.Contains("zaten kullanılıyor", result.Error);
    }

    [Fact]
    public async Task Create_RequiresAdmin()
    {
        using var sut = await CreateAsync();
        sut.SignInAs(sut.Host);

        var result = await sut.Service.CreateAsync(new UserCreateInput(
            "yeni.host", "Yeni Host", UserRole.Host, true, "GizliSifre123", "GizliSifre123"));

        Assert.False(result.Success);
        Assert.Contains("yönetici", result.Error);
    }

    [Fact]
    public async Task Create_HostWithoutLoginStoresPlaceholderCredentials()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(new UserCreateInput(
            "girissiz.host", "Girişsiz Host", UserRole.Host, false, null, null));

        Assert.True(result.Success, result.Error);

        using var db = sut.Db.CreateContext();
        var user = db.Users.Single(x => x.Username == "girissiz.host");
        Assert.False(user.IsLoginEnabled);
        Assert.Equal("!", user.PasswordHash);
    }

    [Fact]
    public async Task Update_LastAdminCannotLoseAdminAccess()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.UpdateAsync(
            sut.Admin.Id,
            new UserUpdateInput("Sistem Yöneticisi", UserRole.Secretary, true, true));

        Assert.False(result.Success);
        Assert.Contains("Son aktif yönetici", result.Error);
    }

    [Fact]
    public async Task Update_CannotDeactivateSelf()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.UpdateAsync(
            sut.Admin.Id,
            new UserUpdateInput("Sistem Yöneticisi", UserRole.Admin, false, true));

        Assert.False(result.Success);
        Assert.Contains("Kendi hesabınızı", result.Error);
    }

    [Fact]
    public async Task Update_DeactivatingHostWritesAuditEntry()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.UpdateAsync(
            sut.Host.Id,
            new UserUpdateInput("Host Bir", UserRole.Host, false, true));

        Assert.True(result.Success, result.Error);
        Assert.Contains(AuditAction.UserDeactivated, sut.Audit.Actions);
    }

    [Fact]
    public async Task ResetPassword_ClearsLockoutAndUpdatesHash()
    {
        using var sut = await CreateAsync();

        string oldHash;
        using (var db = sut.Db.CreateContext())
        {
            var user = db.Users.Single(x => x.Id == sut.Host.Id);
            oldHash = user.PasswordHash;
            user.FailedLoginCount = 5;
            user.LockoutUntilUtc = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
            db.SaveChanges();
        }

        var result = await sut.Service.ResetPasswordAsync(
            sut.Host.Id,
            new UserResetPasswordInput("YeniSifre456", "YeniSifre456"));

        Assert.True(result.Success, result.Error);
        Assert.Contains(AuditAction.PasswordChanged, sut.Audit.Actions);

        using (var db = sut.Db.CreateContext())
        {
            var user = db.Users.Single(x => x.Id == sut.Host.Id);
            Assert.NotEqual(oldHash, user.PasswordHash);
            Assert.Equal(0, user.FailedLoginCount);
            Assert.Null(user.LockoutUntilUtc);
        }
    }

    [Fact]
    public async Task ResetPassword_RejectsMismatchedPasswords()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.ResetPasswordAsync(
            sut.Host.Id,
            new UserResetPasswordInput("YeniSifre456", "BaskaSifre789"));

        Assert.False(result.Success);
        Assert.Contains("eşleşmiyor", result.Error);
    }

    [Fact]
    public async Task GetAll_NonAdminReturnsEmptyList()
    {
        using var sut = await CreateAsync();
        sut.SignInAs(sut.Host);

        var rows = await sut.Service.GetAllAsync();

        Assert.Empty(rows);
    }
}
