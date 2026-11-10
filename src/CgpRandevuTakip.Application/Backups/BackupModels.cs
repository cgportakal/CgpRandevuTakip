namespace CgpRandevuTakip.Application.Backups;

/// <summary>Yedeğin hangi nedenle alındığı.</summary>
public enum BackupTrigger
{
    Manual = 0,
    Automatic = 1,
    PreRestore = 2
}

/// <summary>Yedek geçmişinde gösterilen kayıt.</summary>
public sealed record BackupInfo(
    string Id,
    string FileName,
    string Path,
    DateTime CreatedAtLocal,
    long SizeBytes,
    bool IsVerified,
    string Sha256,
    string Source = "Yerel")
{
    public string SizeText => SizeBytes switch
    {
        >= 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024.0):0.0} MB",
        >= 1024 => $"{SizeBytes / 1024.0:0} KB",
        _ => $"{SizeBytes} B"
    };

    public string CreatedAtText => CreatedAtLocal.ToString("dd/MM/yyyy HH:mm");

    public string VerificationText => IsVerified ? "Doğrulandı" : "Doğrulanmadı";

    public bool IsEncrypted
        => Path.EndsWith(".cgpbak", StringComparison.OrdinalIgnoreCase);

    public string EncryptionText => IsEncrypted ? "Şifreli" : "Şifresiz";

    public bool IsDrive => string.Equals(Source, "Google Drive", StringComparison.OrdinalIgnoreCase);

    public string SourceText => IsDrive ? "Google Drive" : "Yerel";
}

public sealed record BackupManifest(
    string ProductName,
    string Version,
    int SchemaVersion,
    DateTime CreatedAtUtc,
    int UserCount,
    int PersonCount,
    int AppointmentCount,
    string DatabaseSha256,
    string SettingsSha256);

public sealed record BackupCreateResult(
    bool Success,
    string? Error,
    BackupInfo? Backup)
{
    public static BackupCreateResult Ok(BackupInfo backup) => new(true, null, backup);

    public static BackupCreateResult Fail(string error) => new(false, error, null);
}

public sealed record BackupValidationResult(
    bool Success,
    string? Error,
    BackupManifest? Manifest,
    BackupInfo? Backup)
{
    public static BackupValidationResult Ok(BackupManifest manifest, BackupInfo backup)
        => new(true, null, manifest, backup);

    public static BackupValidationResult Fail(string error)
        => new(false, error, null, null);
}

public interface IBackupService
{
    /// <summary>Veritabanı, ayarlar ve manifest içeren ZIP yedeği oluşturur.</summary>
    Task<BackupCreateResult> CreateAsync(BackupTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>Son yedekleri (en yeni önce) döndürür.</summary>
    Task<IReadOnlyList<BackupInfo>> GetHistoryAsync(int count = 50, CancellationToken cancellationToken = default);

    /// <summary>ZIP bütünlüğünü, manifesti ve SHA-256 özetini doğrular.</summary>
    Task<BackupValidationResult> ValidateAsync(string zipPath, CancellationToken cancellationToken = default);

    /// <summary>Yedeği geri yükler. Yalnızca yönetici çağırabilir; önce güvenlik yedeği alır.</summary>
    Task<BackupCreateResult> RestoreAsync(string zipPath, CancellationToken cancellationToken = default);
}
