using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;

namespace CgpRandevuTakip.Infrastructure.Google;

/// <summary>
/// Google Drive for Desktop senkronizasyon klasörüne yedek kopyalar.
/// Bulut yükleme, Drive uygulaması tarafından otomatik yapılır; OAuth gerekmez.
/// </summary>
public sealed class GoogleDriveBackupUploader : IGoogleDriveBackupUploader
{
    private readonly ISettingsService _settings;

    public GoogleDriveBackupUploader(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<OperationResult> UploadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync();
        if (!settings.GoogleDriveEnabled)
            return OperationResult.Ok();

        if (string.IsNullOrWhiteSpace(settings.GoogleDriveFolder))
            return OperationResult.Fail("Google Drive klasörü ayarlanmamış.");

        var folder = settings.GoogleDriveFolder.Trim();
        if (!Directory.Exists(folder))
            return OperationResult.Fail("Google Drive klasörü bulunamadı. Drive for Desktop senkronizasyonunu kontrol edin.");

        try
        {
            var destination = Path.Combine(folder, Path.GetFileName(filePath));
            await using var source = File.OpenRead(filePath);
            await using var target = File.Create(destination);
            await source.CopyToAsync(target, cancellationToken);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Google Drive klasörüne kopyalanamadı: {ex.Message}");
        }
    }
}
