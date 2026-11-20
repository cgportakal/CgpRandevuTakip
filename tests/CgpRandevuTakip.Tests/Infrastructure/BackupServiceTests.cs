using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Backups;
using CgpRandevuTakip.Infrastructure.Persistence;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Infrastructure;

public class BackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _backupDirectory;
    private readonly string _databasePath;
    private readonly FakeSettings _settings = new();
    private readonly CurrentUser _currentUser = new();
    private readonly RecordingAuditLogger _audit = new();

    public BackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CgpBackupTests", Guid.NewGuid().ToString("N"));
        _backupDirectory = Path.Combine(_root, "Yedekler");
        _databasePath = Path.Combine(_root, "data.db");
        Directory.CreateDirectory(_backupDirectory);

        using var db = AppDbContextFactory.Create(_databasePath);
        db.Database.Migrate();

        _settings.Settings = AppSettings.CreateDefault(_backupDirectory) with
        {
            BackupDirectory = _backupDirectory
        };

        _currentUser.SignIn(new UserSession("admin", "admin", "Yönetici", UserRole.Admin));
    }

    private BackupService CreateService()
        => new(
            new SqliteDbContextFactory(_databasePath),
            _settings,
            new SettingsRepository(new SqliteDbContextFactory(_databasePath)),
            new FakeEnvironment(_root, _databasePath, _backupDirectory),
            _audit,
            _currentUser,
            new FixedClock(),
            new NoopGoogleDriveUploader(),
            new FakeSecretProtector());

    [Fact]
    public async Task Create_WritesZipWithManifestSettingsAndHistory()
    {
        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            db.Categories.Add(new Category { Name = "Test Kategori", ColorHex = "#2563EB" });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.CreateAsync(BackupTrigger.Manual);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Backup);
        Assert.True(File.Exists(result.Backup!.Path));
        Assert.StartsWith("CGP_RandevuTakip_Yedek_", result.Backup.FileName);

        using (var zip = ZipFile.OpenRead(result.Backup.Path))
        {
            Assert.NotNull(zip.GetEntry("data.db"));
            Assert.NotNull(zip.GetEntry("settings.json"));
            Assert.NotNull(zip.GetEntry("manifest.json"));

            using var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open(), Encoding.UTF8);
            var manifest = await reader.ReadToEndAsync();
            Assert.Contains("\"AppointmentCount\"", manifest);
            Assert.Contains("\"DatabaseSha256\"", manifest);
        }

        var validation = await service.ValidateAsync(result.Backup.Path);
        Assert.True(validation.Success, validation.Error);
        Assert.Equal(0, validation.Manifest!.AppointmentCount);

        var history = await service.GetHistoryAsync();
        Assert.Single(history);
        Assert.Contains(AuditAction.BackupCreated, _audit.Actions);
    }

    [Fact]
    public async Task Validate_RejectsTamperedDatabase()
    {
        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(created.Success, created.Error);

        var tampered = Path.Combine(_backupDirectory, "bozuk.zip");
        File.Copy(created.Backup!.Path, tampered);

        using (var zip = ZipFile.Open(tampered, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("data.db")!;
            var bytes = Encoding.UTF8.GetBytes("bu bir veritabani degil");
            entry.Delete();
            var replacement = zip.CreateEntry("data.db");
            using var stream = replacement.Open();
            stream.Write(bytes);
        }

        var validation = await service.ValidateAsync(tampered);
        Assert.False(validation.Success);
        Assert.Contains("SHA-256", validation.Error);
    }

    [Fact]
    public async Task Restore_MergesDatabaseContentAndKeepsUsers()
    {
        SeedUser();
        string originalId;
        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            var category = new Category { Name = "Geri Yüklenecek", ColorHex = "#112233" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            originalId = category.Id;
        }

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(created.Success, created.Error);

        // Yedekten sonra eklenen kategori birleştirmede korunmalı.
        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            db.Categories.Add(new Category { Name = "Yedekten Sonra", ColorHex = "#445566" });
            await db.SaveChangesAsync();
        }

        var restored = await service.RestoreAsync(created.Backup!.Path);
        Assert.True(restored.Success, restored.Error);

        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            var names = await db.Categories.Select(x => x.Name).ToListAsync();
            Assert.Contains("Geri Yüklenecek", names);
            Assert.Contains("Yedekten Sonra", names);
            Assert.Equal(2, await db.Categories.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Contains(originalId, await db.Categories.Select(x => x.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task History_IncludesFilesWithoutDatabaseRecord()
    {
        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(created.Success, created.Error);

        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            db.BackupRecords.RemoveRange(db.BackupRecords);
            await db.SaveChangesAsync();
        }

        var copyPath = Path.Combine(
            _backupDirectory,
            "CGP_RandevuTakip_Yedek_2026-11-06_12-30-05.zip");
        File.Copy(created.Backup!.Path, copyPath);

        var history = await service.GetHistoryAsync();
        Assert.Contains(history, x => x.Path == copyPath && x.IsVerified);
        Assert.Contains(history, x => x.Path == created.Backup.Path && x.IsVerified);
    }

    [Fact]
    public async Task Create_EncryptedBackup_CanBeValidatedAndRestored()
    {
        SeedUser();
        _settings.Settings = _settings.Settings with
        {
            BackupEncryptionEnabled = true,
            BackupPassword = "guclu-yedek-parolasi"
        };

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);

        Assert.True(created.Success, created.Error);
        Assert.EndsWith(".cgpbak", created.Backup!.FileName);
        Assert.True(BackupCipher.IsEncryptedFile(created.Backup.Path));
        Assert.True(created.Backup.IsEncrypted);

        var validation = await service.ValidateAsync(created.Backup.Path);
        Assert.True(validation.Success, validation.Error);
        Assert.True(validation.Backup!.IsEncrypted);

        var restored = await service.RestoreAsync(created.Backup.Path);
        Assert.True(restored.Success, restored.Error);
    }

    [Fact]
    public async Task Validate_EncryptedBackup_RejectsWrongPassword()
    {
        _settings.Settings = _settings.Settings with
        {
            BackupEncryptionEnabled = true,
            BackupPassword = "dogru-parola-123"
        };

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(created.Success, created.Error);

        _settings.Settings = _settings.Settings with { BackupPassword = "yanlis-parola-456" };
        var validation = await service.ValidateAsync(created.Backup!.Path);

        Assert.False(validation.Success);
        Assert.Contains("açılamadı", validation.Error);
    }

    [Fact]
    public async Task Create_EncryptedBackup_RequiresPassword()
    {
        _settings.Settings = _settings.Settings with
        {
            BackupEncryptionEnabled = true,
            BackupPassword = string.Empty
        };

        var service = CreateService();
        var result = await service.CreateAsync(BackupTrigger.Manual);

        Assert.False(result.Success);
        Assert.Contains("parola", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AllowsSecretaryAndHostRoles()
    {
        var service = CreateService();

        _currentUser.SignIn(new UserSession("sekreter", "sekreter", "Sekreter", UserRole.Secretary));
        var secretary = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(secretary.Success, secretary.Error);

        _currentUser.SignIn(new UserSession("host", "host", "Görüşmeci", UserRole.Host));
        var host = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(host.Success, host.Error);
    }

    [Fact]
    public async Task Restore_RequiresAdmin()
    {
        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(created.Success, created.Error);

        _currentUser.SignIn(new UserSession("sekreter", "sekreter", "Sekreter", UserRole.Secretary));
        var restored = await service.RestoreAsync(created.Backup!.Path);

        Assert.False(restored.Success);
        Assert.Contains("yönetici", restored.Error);
    }

    [Fact]
    public async Task Inspect_ShowsCountsDatesAndSettings()
    {
        SeedUser();
        SeedSetting(SettingsKeys.SmtpHost, "smtp.example.com");
        SeedSetting(BackupSettingKeys.SyncBaseUrlKey, "http://127.0.0.1:5199");
        SeedAppointment();

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        var inspection = await service.InspectAsync(created.Backup!.Path);
        Assert.True(inspection.Success, inspection.Error);
        Assert.NotNull(inspection.Summary);
        Assert.Equal(1, inspection.Summary!.UserCount);
        Assert.Equal(1, inspection.Summary.AppointmentCount);
        Assert.NotNull(inspection.Summary.LastAppointmentLocal);
        Assert.NotNull(inspection.Summary.LastChangeLocal);
        Assert.True(inspection.Summary.HasSmtpSettings);
        Assert.True(inspection.Summary.HasConnectionSettings);
        Assert.False(inspection.Summary.IsEmpty);
        Assert.True((inspection.AvailableSelection.Sections & BackupSection.Users) != 0);
        Assert.True((inspection.AvailableSelection.Sections & BackupSection.Appointments) != 0);
    }

    [Fact]
    public async Task Create_PartialBackup_ExcludesUsersAndSettings()
    {
        SeedUser();
        SeedSetting(SettingsKeys.SmtpHost, "smtp.example.com");

        var service = CreateService();
        var selection = new BackupSelection(BackupSection.Data, BackupSettingsGroup.None);
        var created = await service.CreateAsync(BackupTrigger.Manual, new BackupOptions(selection));
        Assert.True(created.Success, created.Error);

        var inspection = await service.InspectAsync(created.Backup!.Path);
        Assert.True(inspection.Success, inspection.Error);
        Assert.Equal(0, inspection.Summary!.UserCount);
        Assert.Equal(0, inspection.Summary.SettingCount);
        Assert.True(inspection.Summary.IsEmpty);
    }

    [Fact]
    public async Task Restore_Defaults_PreservesUsersAndSettings()
    {
        SeedUser();
        SeedSetting(SettingsKeys.SmtpHost, "old.example.com");
        var appointment = SeedAppointment();

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        SeedSetting(SettingsKeys.SmtpHost, "new.example.com");
        SeedAppointment("Sonradan Eklenen");

        var restored = await service.RestoreAsync(created.Backup!.Path);
        Assert.True(restored.Success, restored.Error);

        using var db = AppDbContextFactory.Create(_databasePath);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal("new.example.com", (await db.Settings.FindAsync(SettingsKeys.SmtpHost))!.Value);
        Assert.Contains(appointment.AppointmentId, await db.Appointments.Select(x => x.Id).ToListAsync());
        Assert.Equal(2, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Restore_SettingsGroup_OnlyAppliesSelectedGroup()
    {
        SeedUser();
        SeedSetting(SettingsKeys.SmtpHost, "old.example.com");
        SeedSetting(SettingsKeys.BackupDirectory, _backupDirectory);

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        var otherDirectory = Path.Combine(_root, "BaskaYedek");
        SeedSetting(SettingsKeys.SmtpHost, "new.example.com");
        SeedSetting(SettingsKeys.BackupDirectory, otherDirectory);

        var options = new RestoreOptions(
            new BackupSelection(BackupSection.Settings, BackupSettingsGroup.Smtp));
        var restored = await service.RestoreAsync(created.Backup!.Path, options);
        Assert.True(restored.Success, restored.Error);

        using var db = AppDbContextFactory.Create(_databasePath);
        Assert.Equal("old.example.com", (await db.Settings.FindAsync(SettingsKeys.SmtpHost))!.Value);
        Assert.Equal(otherDirectory, (await db.Settings.FindAsync(SettingsKeys.BackupDirectory))!.Value);
    }

    [Fact]
    public async Task Restore_ConnectionSettings_RestoresAddressButNotSessionTokens()
    {
        SeedUser();
        SeedSetting(BackupSettingKeys.SyncBaseUrlKey, "http://old-server:5199");
        SeedSetting("sync.api.access_token", "old-token");

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        SeedSetting(BackupSettingKeys.SyncBaseUrlKey, "http://new-server:5199");
        SeedSetting("sync.api.access_token", "new-token");

        var options = new RestoreOptions(
            new BackupSelection(BackupSection.Settings, BackupSettingsGroup.Connection));
        var restored = await service.RestoreAsync(created.Backup!.Path, options);
        Assert.True(restored.Success, restored.Error);

        using var db = AppDbContextFactory.Create(_databasePath);
        Assert.Equal("http://old-server:5199", (await db.Settings.FindAsync(BackupSettingKeys.SyncBaseUrlKey))!.Value);
        Assert.Equal("new-token", (await db.Settings.FindAsync("sync.api.access_token"))!.Value);
    }

    [Fact]
    public async Task Restore_EmptyBackup_RequiresExplicitConfirmation()
    {
        SeedUser();

        var service = CreateService();
        var emptySelection = new BackupSelection(BackupSection.Data, BackupSettingsGroup.None);
        var created = await service.CreateAsync(BackupTrigger.Manual, new BackupOptions(emptySelection));
        Assert.True(created.Success, created.Error);

        var blocked = await service.RestoreAsync(created.Backup!.Path);
        Assert.False(blocked.Success);
        Assert.Contains("boş", blocked.Error, StringComparison.OrdinalIgnoreCase);

        var allowed = await service.RestoreAsync(
            created.Backup.Path,
            RestoreOptions.Default with { AllowEmptyBackup = true });
        Assert.True(allowed.Success, allowed.Error);

        using var db = AppDbContextFactory.Create(_databasePath);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Restore_ReplaceMode_ReplacesOnlyAppointments()
    {
        SeedUser();
        var first = SeedAppointment("Yedekteki Kişi");

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        var added = SeedAppointment("Sonradan Eklenen");

        var options = RestoreOptions.Default with { AppointmentMode = AppointmentRestoreMode.Replace };
        var restored = await service.RestoreAsync(created.Backup!.Path, options);
        Assert.True(restored.Success, restored.Error);

        using var db = AppDbContextFactory.Create(_databasePath);
        var ids = await db.Appointments.Select(x => x.Id).ToListAsync();
        Assert.Contains(first.AppointmentId, ids);
        Assert.DoesNotContain(added.AppointmentId, ids);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    private void SeedUser(string id = "user-admin", string username = "admin")
    {
        using var db = AppDbContextFactory.Create(_databasePath);
        if (db.Users.Any(x => x.Id == id))
            return;

        db.Users.Add(new User
        {
            Id = id,
            Username = username,
            DisplayName = "Yönetici",
            Role = UserRole.Admin,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            IsLoginEnabled = true,
        });

        db.SaveChanges();
    }

    private void SeedSetting(string key, string value, string? databasePath = null)
    {
        using var db = AppDbContextFactory.Create(databasePath ?? _databasePath);
        var setting = db.Settings.Find(key);
        if (setting is null)
        {
            db.Settings.Add(new Setting { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }

        db.SaveChanges();
    }

    private (string PersonId, string CategoryId, string AppointmentId) SeedAppointment(
        string personName = "Ali Veli",
        DateTime? updatedAtUtc = null)
    {
        using var db = AppDbContextFactory.Create(_databasePath);

        var names = personName.Split(' ', 2);
        var person = new Person
        {
            FirstName = names[0],
            LastName = names.Length > 1 ? names[1] : "Test",
            PhoneE164 = "+905550000000",
        };
        db.Persons.Add(person);

        var category = db.Categories.FirstOrDefault(x => x.Name == "Veli Görüşmesi");
        if (category is null)
        {
            category = new Category { Name = "Veli Görüşmesi", ColorHex = "#2E7D5B" };
            db.Categories.Add(category);
        }

        var appointment = new Appointment
        {
            PersonId = person.Id,
            CategoryId = category.Id,
            HostUserId = "user-admin",
            StartsAtUtc = DateTime.UtcNow.AddDays(1).AddMinutes(Random.Shared.Next(0, 500)),
            DurationMinutes = 30,
            Method = AppointmentMethod.Phone,
            Status = AppointmentStatus.Scheduled,
            UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow,
        };
        db.Appointments.Add(appointment);
        db.SaveChanges();

        return (person.Id, category.Id, appointment.Id);
    }

    [Fact]
    public async Task Inspect_AcceptsVersion1Manifest()
    {
        SeedUser();
        SeedAppointment();

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        using (var zip = ZipFile.Open(created.Backup!.Path, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("manifest.json")!;
            string json;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                json = await reader.ReadToEndAsync();

            var node = JsonNode.Parse(json)!.AsObject();
            node.Remove("ManifestVersion");
            node.Remove("Selection");
            node.Remove("Summary");

            entry.Delete();
            var replacement = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write(node.ToJsonString());
        }

        var validation = await service.ValidateAsync(created.Backup.Path);
        Assert.True(validation.Success, validation.Error);
        Assert.Equal(1, validation.Manifest!.ManifestVersion);

        var inspection = await service.InspectAsync(created.Backup.Path);
        Assert.True(inspection.Success, inspection.Error);
        Assert.Equal(1, inspection.Summary!.AppointmentCount);
        Assert.Equal(1, inspection.Summary.UserCount);
    }

    [Fact]
    public async Task Restore_UsersSection_AddsMissingUserAndKeepsCurrent()
    {
        SeedUser("user-admin", "admin");
        SeedUser("user-host", "host");
        SeedSetting(SettingsKeys.SmtpHost, "smtp.example.com");

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            var host = await db.Users.FindAsync("user-host");
            if (host is not null)
                db.Users.Remove(host);
            await db.SaveChangesAsync();
        }

        var options = new RestoreOptions(
            new BackupSelection(BackupSection.Users, BackupSettingsGroup.None));
        var restored = await service.RestoreAsync(created.Backup!.Path, options);
        Assert.True(restored.Success, restored.Error);

        using var check = AppDbContextFactory.Create(_databasePath);
        Assert.Equal(2, await check.Users.CountAsync());
        Assert.NotNull(await check.Users.FindAsync("user-admin"));
        Assert.NotNull(await check.Users.FindAsync("user-host"));
    }

    [Fact]
    public async Task Create_BackupIncludesConnectionAddressButNeverSessionTokens()
    {
        SeedUser();
        SeedSetting(BackupSettingKeys.SyncBaseUrlKey, "http://server:5199");
        SeedSetting(BackupSettingKeys.SyncReadModeKey, "1");
        SeedSetting("sync.api.access_token", "gizli-token");
        SeedSetting("sync.api.refresh_token", "gizli-refresh");

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual, BackupOptions.Full);
        Assert.True(created.Success, created.Error);

        Assert.Equal("http://server:5199", ReadBackupSetting(created.Backup!.Path, BackupSettingKeys.SyncBaseUrlKey));
        Assert.Equal("1", ReadBackupSetting(created.Backup.Path, BackupSettingKeys.SyncReadModeKey));
        Assert.Null(ReadBackupSetting(created.Backup.Path, "sync.api.access_token"));
        Assert.Null(ReadBackupSetting(created.Backup.Path, "sync.api.refresh_token"));

        var inspection = await service.InspectAsync(created.Backup.Path);
        Assert.True(inspection.Summary!.HasConnectionSettings);
    }

    private static string? ReadBackupSetting(string zipPath, string key)
    {
        var temp = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            $"read-{Guid.NewGuid():N}.db");

        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);

        try
        {
            using (var zip = ZipFile.OpenRead(zipPath))
                zip.GetEntry("data.db")!.ExtractToFile(temp, overwrite: true);

            using var connection = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;Pooling=False");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
            command.Parameters.AddWithValue("$key", key);

            var value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da test sonucu korunur.
        }
    }

    private sealed class NoopGoogleDriveUploader : IGoogleDriveBackupUploader
    {
        public Task<OperationResult> UploadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Ok());
    }

    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Settings { get; set; } = null!;

        public Task<AppSettings> GetAsync() => Task.FromResult(Settings);

        public Task<OperationResult> SaveAsync(AppSettings settings)
        {
            Settings = settings;
            return Task.FromResult(OperationResult.Ok());
        }

        public Task<OperationResult> SetThemeAsync(AppTheme theme)
            => Task.FromResult(OperationResult.Ok());

        public void InvalidateCache()
        {
        }

        public IReadOnlyList<int> AllowedReminderMinutes => new[] { 30, 60, 1440 };
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }

    private sealed class FakeEnvironment : IAppEnvironment
    {
        public FakeEnvironment(string root, string databasePath, string backupDirectory)
        {
            RootDirectory = root;
            DatabasePath = databasePath;
            DefaultBackupDirectory = backupDirectory;
        }

        public string RootDirectory { get; }
        public string DatabasePath { get; }
        public string LogsDirectory => Path.Combine(RootDirectory, "logs");
        public string DefaultBackupDirectory { get; }
        public string BrandingDirectory => Path.Combine(RootDirectory, "branding");
    }
}
