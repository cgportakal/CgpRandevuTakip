using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class SettingsServiceTests
{
    private static (SettingsService Service, CurrentUser CurrentUser, RecordingAuditLogger Audit, TestAppEnvironment Environment, TestDatabase Db)
        CreateSut(UserRole role = UserRole.Admin)
    {
        var db = new TestDatabase();
        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("user-1", "test", "Test Kullanıcı", role));
        var audit = new RecordingAuditLogger();
        var environment = new TestAppEnvironment();

        var service = new SettingsService(
            new SettingsRepository(db.Factory),
            audit,
            currentUser,
            environment,
            new NoopSecretProtector());

        return (service, currentUser, audit, environment, db);
    }

    [Fact]
    public async Task Get_ReturnsDocumentedDefaults()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var settings = await service.GetAsync();

            Assert.Equal(AppTheme.Auto, settings.Theme);
            Assert.Equal("BEK Okulları", settings.InstitutionName);
            Assert.Equal(environment.DefaultBackupDirectory, settings.BackupDirectory);
            Assert.True(settings.NotificationsEnabled);
            Assert.Equal(30, settings.DefaultReminderMinutes);
            Assert.False(settings.PushEnabled);
            Assert.Equal("ntfy", settings.PushProvider);
            Assert.Equal("08:30", settings.DailyShareTime);
            Assert.True(settings.QuietHoursEnabled);
            Assert.Equal("22:00", settings.QuietHoursStart);
            Assert.Equal("07:00", settings.QuietHoursEnd);
            Assert.True(settings.AutoStartEnabled);
            Assert.Equal("tray", settings.AutoStartMode);
            Assert.True(settings.CloseToTray);
            Assert.Equal(string.Empty, settings.InstitutionEmail);
            Assert.Equal(string.Empty, settings.InstitutionPhone);
            Assert.Equal(string.Empty, settings.InstitutionAddress);
            Assert.Equal(string.Empty, settings.InstitutionLogoFile);
        }
    }

    [Fact]
    public async Task Save_PersistsAllSettings()
    {
        var (service, _, audit, environment, db) = CreateSut();
        using (db)
        {
            var update = new AppSettings(
                AppTheme.Dark,
                "CGP Koleji",
                environment.DefaultBackupDirectory,
                NotificationsEnabled: false,
                DefaultReminderMinutes: 60,
                PushEnabled: true,
                PushProvider: "telegram",
                PushTopic: "cgp-randevu",
                DailyShareTime: "09:15");

            var result = await service.SaveAsync(update);
            Assert.True(result.Success, result.Error);

            var saved = await service.GetAsync();
            Assert.Equal(AppTheme.Dark, saved.Theme);
            Assert.Equal("CGP Koleji", saved.InstitutionName);
            Assert.False(saved.NotificationsEnabled);
            Assert.Equal(60, saved.DefaultReminderMinutes);
            Assert.True(saved.PushEnabled);
            Assert.Equal("telegram", saved.PushProvider);
            Assert.Equal("cgp-randevu", saved.PushTopic);
            Assert.Equal("09:15", saved.DailyShareTime);
            Assert.Contains(AuditAction.ThemeChanged, audit.Actions);
        }
    }

    [Fact]
    public async Task Save_PersistsQuietHoursAndAppBehavior()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                QuietHoursEnabled = true,
                QuietHoursStart = "23:15",
                QuietHoursEnd = "06:45",
                AutoStartEnabled = true,
                AutoStartMode = "tray",
                CloseToTray = false
            };

            var result = await service.SaveAsync(update);
            Assert.True(result.Success, result.Error);

            var saved = await service.GetAsync();
            Assert.True(saved.QuietHoursEnabled);
            Assert.Equal("23:15", saved.QuietHoursStart);
            Assert.Equal("06:45", saved.QuietHoursEnd);
            Assert.True(saved.AutoStartEnabled);
            Assert.Equal("tray", saved.AutoStartMode);
            Assert.False(saved.CloseToTray);
        }
    }

    [Fact]
    public async Task Save_PersistsInstitutionProfileAndLogo()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                InstitutionName = "BEK Okulları",
                InstitutionEmail = "info@bek.example",
                InstitutionPhone = "0532 123 45 67",
                InstitutionAddress = "Atatürk Mahallesi Okul Caddesi No: 1, Ankara",
                InstitutionLogoFile = "branding/institution-logo.png"
            };

            var result = await service.SaveAsync(update);
            Assert.True(result.Success, result.Error);

            var saved = await service.GetAsync();
            Assert.Equal("info@bek.example", saved.InstitutionEmail);
            Assert.Equal("0532 123 45 67", saved.InstitutionPhone);
            Assert.StartsWith("Atatürk", saved.InstitutionAddress);
            Assert.Equal("branding/institution-logo.png", saved.InstitutionLogoFile);
        }
    }

    [Fact]
    public async Task Save_InvalidInstitutionEmail_Fails()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                InstitutionEmail = "gecersiz-adres"
            };

            var result = await service.SaveAsync(update);

            Assert.False(result.Success);
            Assert.Contains("Kurum e-posta", result.Error);
        }
    }

    [Fact]
    public async Task Save_InvalidQuietHours_Fails()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                QuietHoursStart = "25:00"
            };

            var result = await service.SaveAsync(update);

            Assert.False(result.Success);
            Assert.Contains("Sessiz saatler", result.Error);
        }
    }

    [Fact]
    public async Task Save_NonAdmin_Fails()
    {
        var (service, _, _, environment, db) = CreateSut(UserRole.Secretary);
        using (db)
        {
            var result = await service.SaveAsync(
                AppSettings.CreateDefault(environment.DefaultBackupDirectory));

            Assert.False(result.Success);
            Assert.Contains("yönetici", result.Error);
        }
    }

    [Fact]
    public async Task SetTheme_NonAdmin_IsAllowedAndAudited()
    {
        var (service, _, audit, _, db) = CreateSut(UserRole.Host);
        using (db)
        {
            var result = await service.SetThemeAsync(AppTheme.Light);

            Assert.True(result.Success, result.Error);
            Assert.Equal(AppTheme.Light, (await service.GetAsync()).Theme);
            Assert.Contains(AuditAction.ThemeChanged, audit.Actions);
        }
    }

    [Theory]
    [InlineData(45)]
    [InlineData(0)]
    public async Task Save_InvalidReminder_Fails(int minutes)
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                DefaultReminderMinutes = minutes
            };

            var result = await service.SaveAsync(update);

            Assert.False(result.Success);
            Assert.Contains("hatırlatıcı", result.Error);
        }
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("9:15")]
    [InlineData("ab:cd")]
    public async Task Save_InvalidDailyTime_Fails(string time)
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                DailyShareTime = time
            };

            var result = await service.SaveAsync(update);

            Assert.False(result.Success);
            Assert.Contains("saat", result.Error);
        }
    }

    [Fact]
    public async Task Save_UnwritableBackupDirectory_Fails()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                BackupDirectory = "C:\\gecersiz\0klasor"
            };

            var result = await service.SaveAsync(update);

            Assert.False(result.Success);
            Assert.Contains("yazılamıyor", result.Error);
        }
    }

    [Fact]
    public async Task Save_UnknownPushProvider_NormalizesToNtfy()
    {
        var (service, _, _, environment, db) = CreateSut();
        using (db)
        {
            var update = AppSettings.CreateDefault(environment.DefaultBackupDirectory) with
            {
                PushProvider = "sms"
            };

            var result = await service.SaveAsync(update);

            Assert.True(result.Success, result.Error);
            Assert.Equal("ntfy", (await service.GetAsync()).PushProvider);
        }
    }
}
