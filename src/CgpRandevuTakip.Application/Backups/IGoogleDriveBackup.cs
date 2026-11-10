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

/// <summary>Tarayıcı üzerinden Google OAuth bağlantısını kurar.</summary>
public interface IGoogleDriveAuthService
{
    Task<GoogleDriveAuthResult> ConnectAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default);
}
