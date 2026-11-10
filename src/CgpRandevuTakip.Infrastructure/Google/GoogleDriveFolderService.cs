using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Infrastructure.Google;

/// <summary>Drive for Desktop klasöründeki yedekleri listeler ve yerel klasöre indirir.</summary>
public sealed class GoogleDriveFolderService : IGoogleDriveFolderService
{
    private readonly ISettingsService _settings;

    public GoogleDriveFolderService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync();
        if (!settings.GoogleDriveEnabled || string.IsNullOrWhiteSpace(settings.GoogleDriveFolder))
            return Array.Empty<BackupInfo>();

        var folder = settings.GoogleDriveFolder.Trim();
        if (!Directory.Exists(folder))
            return Array.Empty<BackupInfo>();

        return Directory
            .GetFiles(folder, BackupFileNameBuilder.Prefix + "*")
            .Where(x => BackupFileNameBuilder.IsValid(Path.GetFileName(x)))
            .Select(x => new BackupInfo(
                string.Empty,
                Path.GetFileName(x),
                x,
                File.GetLastWriteTime(x),
                new FileInfo(x).Length,
                false,
                string.Empty,
                "Google Drive"))
            .OrderByDescending(x => x.CreatedAtLocal)
            .ToList();
    }

    public async Task<(string? Path, string? Error)> DownloadAsync(
        string drivePath,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        try
        {
            if (!File.Exists(drivePath))
                return (null, "Drive yedeği bulunamadı.");

            var settings = await _settings.GetAsync();
            var localFolder = string.IsNullOrWhiteSpace(settings.BackupDirectory)
                ? Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", "Yedekler")
                : settings.BackupDirectory;

            Directory.CreateDirectory(localFolder);
            var destination = Path.Combine(localFolder, Path.GetFileName(drivePath));

            if (string.Equals(
                    Path.GetFullPath(destination),
                    Path.GetFullPath(drivePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return (destination, null);
            }

            File.Copy(drivePath, destination, overwrite: true);
            return (destination, null);
        }
        catch (Exception ex)
        {
            return (null, $"Drive yedeği indirilemedi: {ex.Message}");
        }
    }
}
