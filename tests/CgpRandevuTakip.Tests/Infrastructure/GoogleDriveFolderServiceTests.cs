using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Infrastructure.Google;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Infrastructure;

public class GoogleDriveFolderServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _driveFolder;
    private readonly string _backupFolder;
    private readonly FakeSettings _settings = new();

    public GoogleDriveFolderServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CgpDriveTests", Guid.NewGuid().ToString("N"));
        _driveFolder = Path.Combine(_root, "Drive");
        _backupFolder = Path.Combine(_root, "Yedekler");
        Directory.CreateDirectory(_driveFolder);
        Directory.CreateDirectory(_backupFolder);

        _settings.Settings = AppSettings.CreateDefault(_backupFolder) with
        {
            GoogleDriveEnabled = true,
            GoogleDriveFolder = _driveFolder,
            BackupDirectory = _backupFolder
        };
    }

    [Fact]
    public async Task Upload_List_Download_RoundTrips()
    {
        var source = Path.Combine(_backupFolder, "CGP_RandevuTakip_Yedek_2026-11-11_10-00-00.cgpbak");
        await File.WriteAllTextAsync(source, "yedek içeriği");

        var uploader = new GoogleDriveBackupUploader(_settings);
        var uploaded = await uploader.UploadAsync(source);
        Assert.True(uploaded.Success, uploaded.Error);

        var driveCopy = Path.Combine(_driveFolder, Path.GetFileName(source));
        Assert.True(File.Exists(driveCopy));

        var folderService = new GoogleDriveFolderService(_settings);
        var list = await folderService.ListAsync();
        Assert.Single(list);
        Assert.True(list[0].IsDrive);
        Assert.Equal("Google Drive", list[0].SourceText);

        var download = await folderService.DownloadAsync(driveCopy);
        Assert.NotNull(download.Path);
        Assert.True(File.Exists(download.Path));

        File.Delete(driveCopy);
        var missing = await folderService.DownloadAsync(driveCopy);
        Assert.Null(missing.Path);
        Assert.Contains("bulunamadı", missing.Error);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonuç korunur.
        }
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
}
