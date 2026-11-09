using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Recipients;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class RecipientServiceTests
{
    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required RecipientService Service { get; init; }

        public void SignIn(UserRole role)
            => CurrentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));

        public void Dispose() => Db.Dispose();
    }

    private static Sut Create(UserRole role = UserRole.Secretary)
    {
        var db = new TestDatabase();
        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));
        var service = new RecipientService(
            new RecipientRepository(db.Factory),
            currentUser,
            new RecordingAuditLogger());

        return new Sut { Db = db, CurrentUser = currentUser, Service = service };
    }

    [Fact]
    public async Task Create_ThenGetActive_ReturnsRecipient()
    {
        using var sut = Create();

        var created = await sut.Service.CreateAsync(
            new RecipientInput("Ayşe Yılmaz", "0532 123 45 67", "Yönetim", true));
        Assert.True(created.Success, created.Error);

        var rows = await sut.Service.GetActiveAsync();
        var row = Assert.Single(rows);
        Assert.Equal("Ayşe Yılmaz", row.Name);
        Assert.Equal("+905321234567", row.PhoneE164);
        Assert.Equal("Yönetim", row.Tag);
    }

    [Fact]
    public async Task Update_CanDeactivateRecipient()
    {
        using var sut = Create();
        await sut.Service.CreateAsync(new RecipientInput("Ayşe", "0532 123 45 67", null, true));
        var row = (await sut.Service.GetAllAsync()).Single();

        var updated = await sut.Service.UpdateAsync(
            row.Id,
            new RecipientInput("Ayşe Yılmaz", "0532 123 45 67", null, false));
        Assert.True(updated.Success, updated.Error);

        Assert.Empty(await sut.Service.GetActiveAsync());
        Assert.Single(await sut.Service.GetAllAsync());
    }

    [Fact]
    public async Task Create_InvalidPhone_Fails()
    {
        using var sut = Create();

        var result = await sut.Service.CreateAsync(
            new RecipientInput("Ali", "abc", null, true));

        Assert.False(result.Success);
        Assert.Contains("Telefon", result.Error);
    }

    [Fact]
    public async Task HostRole_CannotManageRecipients()
    {
        using var sut = Create(UserRole.Host);

        var result = await sut.Service.CreateAsync(
            new RecipientInput("Ali", "0532 123 45 67", null, true));

        Assert.False(result.Success);
        Assert.Empty(await sut.Service.GetAllAsync());
    }
}
