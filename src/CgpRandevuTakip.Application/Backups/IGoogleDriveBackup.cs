using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Backups;

/// <summary>Yerel yedeği Google Drive'a yükler (yedek şifreli olmalıdır).</summary>
public interface IGoogleDriveBackupUploader
{
    Task<OperationResult> UploadAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed record GoogleDriveAuthResult(
    bool Success,
    string? Error,
    string? RefreshToken,
    string? Email)
{
    public static GoogleDriveAuthResult Ok(string refreshToken, string email)
        => new(true, null, refreshToken, email);

    public static GoogleDriveAuthResult Fail(string error)
        => new(false, error, null, null);
}

/// <summary>
/// Google Drive for Desktop ile senkronize edilen yerel klasörü yönetir.
/// Ayrı OAuth bağlantısı gerektirmez.
/// </summary>
public interface IGoogleDriveFolderService
{
    Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Drive klasöründeki yedeği yerel yedek klasörüne indirir.</summary>
    Task<(string? Path, string? Error)> DownloadAsync(
        string drivePath,
        CancellationToken cancellationToken = default);
}

/// <summary>Tarayıcı üzerinden Google OAuth bağlantısını kurar.</summary>
public interface IGoogleDriveAuthService
{
    Task<GoogleDriveAuthResult> ConnectAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default);
}
