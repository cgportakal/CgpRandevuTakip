using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Infrastructure.Security;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class AuthServiceTests
{
    private static (AuthService Service, CurrentUser CurrentUser, FixedClock Clock, RecordingAuditLogger Audit, TestDatabase Db)
        CreateSut()
    {
        var db = new TestDatabase();
        var currentUser = new CurrentUser();
        var clock = new FixedClock();
        var audit = new RecordingAuditLogger();
        var service = new AuthService(
            new UserRepository(db.Factory),
            new Pbkdf2PasswordHasher(),
            clock,
            audit,
            currentUser);

        return (service, currentUser, clock, audit, db);
    }

    private static SetupRequest ValidSetup() => new(
        "ayse.yilmaz",
        "Ayşe Yılmaz",
        "ayse@example.com",
        "GizliSifre123",
        "GizliSifre123");

    [Fact]
    public async Task CreateFirstAdmin_CreatesHashedAdminAndSignsIn()
    {
        var (service, currentUser, clock, audit, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateFirstAdminAsync(ValidSetup());

            Assert.True(result.Success, result.Error);
            Assert.True(currentUser.IsAuthenticated);
            Assert.Equal(UserRole.Admin, currentUser.Role);
            Assert.Equal("Ayşe Yılmaz", currentUser.DisplayName);

            using var context = db.CreateContext();
            var user = context.Users.Single();
            Assert.Equal("ayse.yilmaz", user.Username);
            Assert.Equal(UserRole.Admin, user.Role);
            Assert.NotEqual("GizliSifre123", user.PasswordHash);
            Assert.NotEmpty(user.PasswordSalt);
            Assert.Equal(clock.UtcNow, user.LastLoginAtUtc);
            Assert.Contains(AuditAction.FirstRunCompleted, audit.Actions);
        }
    }

    [Fact]
    public async Task RememberToken_LogsInWithoutPassword()
    {
        var (service, currentUser, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            var token = await service.CreateRememberTokenAsync();
            Assert.False(string.IsNullOrWhiteSpace(token));

            currentUser.SignOut();
            Assert.False(currentUser.IsAuthenticated);

            var result = await service.LoginWithRememberTokenAsync(token!);

            Assert.True(result.Success, result.Error);
            Assert.True(currentUser.IsAuthenticated);
            Assert.Equal("ayse.yilmaz", currentUser.Username);
        }
    }

    [Fact]
    public async Task RememberToken_IsInvalidatedByPasswordChange()
    {
        var (service, currentUser, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            var token = await service.CreateRememberTokenAsync();
            Assert.NotNull(token);

            var changed = await service.ChangePasswordAsync(
                "GizliSifre123", "YeniSifre123", "YeniSifre123");
            Assert.True(changed.Success, changed.Error);

            currentUser.SignOut();
            var result = await service.LoginWithRememberTokenAsync(token!);

            Assert.False(result.Success);
            Assert.False(currentUser.IsAuthenticated);
        }
    }

    [Fact]
    public async Task CreateFirstAdmin_WhenAlreadyExists_ReturnsFailure()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            var second = await service.CreateFirstAdminAsync(new SetupRequest(
                "ikinci.admin", "İkinci Yönetici", "ikinci@example.com", "BaskaSifre123", "BaskaSifre123"));

            Assert.False(second.Success);
            Assert.Contains("tamamlanmış", second.Error);
        }
    }

    [Theory]
    [InlineData("kisa1")]
    [InlineData("sadeceharf")]
    [InlineData("1234567890")]
    public async Task CreateFirstAdmin_RejectsWeakPassword(string password)
    {
        var (service, currentUser, _, _, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateFirstAdminAsync(new SetupRequest(
                "admin", "Yönetici", "admin@example.com", password, password));

            Assert.False(result.Success);
            Assert.False(currentUser.IsAuthenticated);
        }
    }

    [Fact]
    public async Task CreateFirstAdmin_RejectsMismatchedPasswords()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateFirstAdminAsync(new SetupRequest(
                "admin", "Yönetici", "admin@example.com", "GizliSifre123", "BaskaSifre123"));

            Assert.False(result.Success);
            Assert.Contains("eşleşmiyor", result.Error);
        }
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("boşluk lu")]
    [InlineData("türkçe.İ")]
    public async Task CreateFirstAdmin_RejectsInvalidUsername(string username)
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateFirstAdminAsync(new SetupRequest(
                username, "Yönetici", "admin@example.com", "GizliSifre123", "GizliSifre123"));

            Assert.False(result.Success);
        }
    }

    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        var (service, currentUser, _, audit, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            await service.LogoutAsync();
            audit.Actions.Clear();

            var result = await service.LoginAsync(" Ayse.Yilmaz ", "GizliSifre123");

            Assert.True(result.Success, result.Error);
            Assert.True(currentUser.IsAuthenticated);
            Assert.Equal("ayse.yilmaz", result.Session!.Username);
            Assert.Contains(AuditAction.LoginSucceeded, audit.Actions);
        }
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGenericErrorAndCountsAttempt()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            await service.LogoutAsync();

            var result = await service.LoginAsync("ayse.yilmaz", "YanlisSifre123");

            Assert.False(result.Success);
            Assert.Equal("Kullanıcı adı veya şifre hatalı. Kalan deneme hakkı: 4.", result.Error);

            using var context = db.CreateContext();
            Assert.Equal(1, context.Users.Single().FailedLoginCount);
        }
    }

    [Fact]
    public async Task Login_FiveFailures_LocksAccountForFifteenMinutes()
    {
        var (service, _, clock, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            await service.LogoutAsync();

            LoginResult? last = null;
            for (var i = 0; i < 5; i++)
                last = await service.LoginAsync("ayse.yilmaz", "YanlisSifre123");

            Assert.NotNull(last);
            Assert.False(last!.Success);
            Assert.Equal(AuthService.LockoutMinutes, last.LockoutRemainingMinutes);

            using (var context = db.CreateContext())
            {
                var user = context.Users.Single();
                Assert.NotNull(user.LockoutUntilUtc);
                Assert.Equal(clock.UtcNow.AddMinutes(15), user.LockoutUntilUtc);
            }

            var locked = await service.LoginAsync("ayse.yilmaz", "GizliSifre123");
            Assert.False(locked.Success);
            Assert.Contains("kilitli", locked.Error);

            clock.UtcNow = clock.UtcNow.AddMinutes(16);
            var afterLockout = await service.LoginAsync("ayse.yilmaz", "GizliSifre123");
            Assert.True(afterLockout.Success, afterLockout.Error);
        }
    }

    [Fact]
    public async Task Login_DisabledUser_FailsWithoutRevealingReason()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            await service.LogoutAsync();

            using (var context = db.CreateContext())
            {
                var user = context.Users.Single();
                user.IsActive = false;
                context.SaveChanges();
            }

            var result = await service.LoginAsync("ayse.yilmaz", "GizliSifre123");

            Assert.False(result.Success);
            Assert.Equal("Kullanıcı adı veya şifre hatalı.", result.Error);
        }
    }

    [Fact]
    public async Task ChangePassword_UpdatesHashAndOldPasswordStopsWorking()
    {
        var (service, _, _, audit, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());

            var result = await service.ChangePasswordAsync(
                "GizliSifre123", "YeniSifre456", "YeniSifre456");
            Assert.True(result.Success, result.Error);
            Assert.Contains(AuditAction.PasswordChanged, audit.Actions);

            await service.LogoutAsync();
            Assert.False((await service.LoginAsync("ayse.yilmaz", "GizliSifre123")).Success);
            Assert.True((await service.LoginAsync("ayse.yilmaz", "YeniSifre456")).Success);
        }
    }

    [Fact]
    public async Task ChangePassword_RejectsWrongCurrentPassword()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());

            var result = await service.ChangePasswordAsync(
                "YanlisSifre123", "YeniSifre456", "YeniSifre456");

            Assert.False(result.Success);
            Assert.Contains("Mevcut şifre", result.Error);
        }
    }

    [Fact]
    public async Task Logout_SignsOutCurrentUser()
    {
        var (service, currentUser, _, audit, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            await service.LogoutAsync();

            Assert.False(currentUser.IsAuthenticated);
            Assert.Contains(AuditAction.Logout, audit.Actions);
        }
    }

    [Fact]
    public async Task RememberToken_HasNoExpiryAndSurvivesLongTime()
    {
        var (service, currentUser, clock, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            var token = await service.CreateRememberTokenAsync();
            Assert.NotNull(token);

            var user = await new UserRepository(db.Factory).FindByRememberTokenHashAsync(
                Sha256Hex(token!));
            Assert.NotNull(user);
            Assert.Null(user!.RememberTokenExpiresAtUtc);

            currentUser.SignOut();
            clock.UtcNow = clock.UtcNow.AddYears(5);

            var result = await service.LoginWithRememberTokenAsync(token!);

            Assert.True(result.Success, result.Error);
            Assert.True(currentUser.IsAuthenticated);
        }
    }

    [Fact]
    public async Task Logout_RevokesRememberToken()
    {
        var (service, currentUser, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());
            var token = await service.CreateRememberTokenAsync();
            Assert.NotNull(token);

            await service.LogoutAsync();

            var result = await service.LoginWithRememberTokenAsync(token!);
            Assert.False(result.Success);
            Assert.False(currentUser.IsAuthenticated);
        }
    }

    [Fact]
    public async Task CreateFirstAdmin_RequiresValidEmail()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            var invalid = await service.CreateFirstAdminAsync(
                ValidSetup() with { Email = "gecersiz-adres" });
            Assert.False(invalid.Success);
            Assert.Contains("e-posta", invalid.Error, StringComparison.OrdinalIgnoreCase);

            var missing = await service.CreateFirstAdminAsync(
                ValidSetup() with { Email = "" });
            Assert.False(missing.Success);
        }
    }

    [Fact]
    public async Task CreateFirstAdmin_SavesNormalizedEmail()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateFirstAdminAsync(
                ValidSetup() with { Email = "Ayse@Example.com" });
            Assert.True(result.Success, result.Error);

            var user = await new UserRepository(db.Factory).FindByUsernameAsync("ayse.yilmaz");
            Assert.Equal("ayse@example.com", user!.Email);
        }
    }

    [Fact]
    public async Task UpdateAdminEmail_NormalizesAndSaves()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());

            var result = await service.UpdateAdminEmailAsync("Yeni@Example.com");

            Assert.True(result.Success, result.Error);
            var user = await new UserRepository(db.Factory).FindByUsernameAsync("ayse.yilmaz");
            Assert.Equal("yeni@example.com", user!.Email);
        }
    }

    [Fact]
    public async Task UpdateAdminEmail_RejectsInvalidAddress()
    {
        var (service, _, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateFirstAdminAsync(ValidSetup());

            var result = await service.UpdateAdminEmailAsync("gecersiz");

            Assert.False(result.Success);
        }
    }

    private static string Sha256Hex(string value)
        => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("GizliSifre123")]
    [InlineData("abcdefg1")]
    public void Validate_AcceptsValidPasswords(string password)
        => Assert.Null(PasswordPolicy.Validate(password));

    [Theory]
    [InlineData("")]
    [InlineData("kisa1")]
    [InlineData("sadeceharfler")]
    [InlineData("1234567890")]
    public void Validate_RejectsInvalidPasswords(string password)
        => Assert.NotNull(PasswordPolicy.Validate(password));
}
