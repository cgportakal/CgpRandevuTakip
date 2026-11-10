using System.IO.Compression;
using System.Text;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Backups;
using CgpRandevuTakip.Infrastructure.Persistence;
using CgpRandevuTakip.Tests.Support;
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
            new FakeEnvironment(_root, _databasePath, _backupDirectory),
            _audit,
            _currentUser,
            new FixedClock(),
            new NoopGoogleDriveUploader());

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
    public async Task Restore_BringsBackDatabaseContent()
    {
        Guid originalId;
        using (var db = AppDbContextFactory.Create(_databasePath))
        {
            var category = new Category { Name = "Geri Yüklenecek", ColorHex = "#112233" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            originalId = Guid.Parse(category.Id);
        }

        var service = CreateService();
        var created = await service.CreateAsync(BackupTrigger.Manual);
        Assert.True(created.Success, created.Error);

        // Yedekten sonra eklenen kategori geri yüklemede kaybolmalı.
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
            Assert.DoesNotContain("Yedekten Sonra", names);
            Assert.Equal(1, await db.Categories.CountAsync());
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

        public IReadOnlyList<int> AllowedReminderMinutes => new[] { 30, 60, 1440 };
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
    }
}
