using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Infrastructure.Security;
using CgpRandevuTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Application;

public class HostServiceTests
{
    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required RecordingAuditLogger Audit { get; init; }
        public required HostService Service { get; init; }

        public void SignInAsRole(UserRole role)
            => CurrentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));

        public void Dispose() => Db.Dispose();
    }

    private static Task<Sut> CreateAsync(UserRole role = UserRole.Admin)
    {
        var db = new TestDatabase();
        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));
        var audit = new RecordingAuditLogger();

        var service = new HostService(
            new UserRepository(db.Factory),
            new AppointmentRepository(db.Factory),
            new Pbkdf2PasswordHasher(),
            audit,
            currentUser);

        return Task.FromResult(new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Audit = audit,
            Service = service
        });
    }

    [Fact]
    public async Task Create_WithoutLogin_GeneratesUsernameAndDisablesLogin()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(new HostCreateInput(
            "Ayşe Yılmaz", LoginEnabled: false, Username: null, Password: null, ConfirmPassword: null));

        Assert.True(result.Success, result.Error);
        Assert.Contains(AuditAction.UserCreated, sut.Audit.Actions);

        var hosts = await sut.Service.GetAllAsync();
        var host = Assert.Single(hosts);
        Assert.Equal("Ayşe Yılmaz", host.DisplayName);
        Assert.Equal("host-ayseyilmaz", host.Username);
        Assert.False(host.IsLoginEnabled);
        Assert.True(host.IsActive);
        Assert.Equal(UserRole.Host, host.Role);
        Assert.Equal(0, host.AppointmentCount);
    }

    [Fact]
    public async Task Create_GeneratesUniqueUsernameForSameName()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(new HostCreateInput("Mehmet Kaya", false, null, null, null));
        await sut.Service.CreateAsync(new HostCreateInput("Mehmet Kaya", false, null, null, null));

        var hosts = await sut.Service.GetAllAsync();
        Assert.Equal(2, hosts.Count);
        Assert.Contains(hosts, x => x.Username == "host-mehmetkaya");
        Assert.Contains(hosts, x => x.Username == "host-mehmetkaya-2");
    }

    [Fact]
    public async Task Create_WithLogin_StoresHashedPassword()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(new HostCreateInput(
            "Zeynep Demir", true, "zeynep.demir", "Gizli1234", "Gizli1234"));

        Assert.True(result.Success, result.Error);

        using var db = sut.Db.CreateContext();
        var user = db.Users.Single(x => x.Username == "zeynep.demir");
        Assert.True(user.IsLoginEnabled);
        Assert.NotEqual("Gizli1234", user.PasswordHash);
        Assert.True(new Pbkdf2PasswordHasher().Verify(
            "Gizli1234", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
    }

    [Fact]
    public async Task Create_RejectsDuplicateUsername()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(new HostCreateInput(
            "Zeynep Demir", true, "zeynep.demir", "Gizli1234", "Gizli1234"));

        var duplicate = await sut.Service.CreateAsync(new HostCreateInput(
            "Başka Kişi", true, "Zeynep.Demir", "Gizli1234", "Gizli1234"));

        Assert.False(duplicate.Success);
        Assert.Contains("zaten kullanılıyor", duplicate.Error);
    }

    [Fact]
    public async Task Create_RejectsMismatchedPasswords()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(new HostCreateInput(
            "Zeynep Demir", true, "zeynep.demir", "Gizli1234", "Gizli4321"));

        Assert.False(result.Success);
        Assert.Contains("eşleşmiyor", result.Error);
    }

    [Fact]
    public async Task Create_RequiresAdmin()
    {
        using var sut = await CreateAsync(UserRole.Secretary);

        var result = await sut.Service.CreateAsync(new HostCreateInput(
            "Ayşe Yılmaz", false, null, null, null));

        Assert.False(result.Success);
        Assert.Contains("yönetici", result.Error);
    }

    [Fact]
    public async Task Update_ChangesDisplayNameAndActiveState()
    {
        using var sut = await CreateAsync();
        await sut.Service.CreateAsync(new HostCreateInput("Ayşe Yılmaz", false, null, null, null));
        var host = (await sut.Service.GetAllAsync()).Single();

        var result = await sut.Service.UpdateAsync(host.Id, new HostUpdateInput("Ayşe Kaya", false));

        Assert.True(result.Success, result.Error);
        var updated = (await sut.Service.GetAllAsync()).Single();
        Assert.Equal("Ayşe Kaya", updated.DisplayName);
        Assert.False(updated.IsActive);
        Assert.Contains(AuditAction.UserDeactivated, sut.Audit.Actions);
    }

    [Fact]
    public async Task GetSelectableHosts_ExcludesInactive()
    {
        using var sut = await CreateAsync();
        await sut.Service.CreateAsync(new HostCreateInput("Aktif Kişi", false, null, null, null));
        await sut.Service.CreateAsync(new HostCreateInput("Pasif Kişi", false, null, null, null));

        var all = await sut.Service.GetAllAsync();
        var passive = all.Single(x => x.DisplayName == "Pasif Kişi");
        await sut.Service.UpdateAsync(passive.Id, new HostUpdateInput("Pasif Kişi", false));

        var selectable = await sut.Service.GetSelectableHostsAsync();

        var only = Assert.Single(selectable);
        Assert.Equal("Aktif Kişi", only.DisplayName);
    }

    [Fact]
    public async Task Update_RejectsDeactivatingLastAdmin()
    {
        using var sut = await CreateAsync();

        var admin = new User
        {
            Username = "admin",
            DisplayName = "Yönetici",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        using (var db = sut.Db.CreateContext())
        {
            db.Users.Add(admin);
            db.SaveChanges();
        }

        sut.CurrentUser.SignIn(new UserSession("actor", "actor", "Aktör", UserRole.Admin));
        var result = await sut.Service.UpdateAsync(admin.Id, new HostUpdateInput("Yönetici", false));

        Assert.False(result.Success);
        Assert.Contains("Son aktif yönetici", result.Error);
    }
}
