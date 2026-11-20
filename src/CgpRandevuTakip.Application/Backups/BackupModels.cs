using System.Text.Json.Serialization;
using CgpRandevuTakip.Application.Settings;

namespace CgpRandevuTakip.Application.Backups;

/// <summary>Yedeğin hangi nedenle alındığı.</summary>
public enum BackupTrigger
{
    Manual = 0,
    Automatic = 1,
    PreRestore = 2
}

/// <summary>Yedeğe/geri yüklemeye dahil edilebilecek veri bölümleri.</summary>
[Flags]
public enum BackupSection
{
    None = 0,
    Appointments = 1,
    Persons = 2,
    Categories = 4,
    Users = 8,
    Settings = 16,
    AuditLogs = 32,
    ShareLogs = 64,
    BackupHistory = 128,
    ContentVersions = 256,
    Recipients = 512,
    Logo = 1024,

    Data = Appointments | Persons | Categories,
    All = Data | Users | Settings | AuditLogs | ShareLogs | BackupHistory
          | ContentVersions | Recipients | Logo
}

/// <summary>Ayarların geri yüklenebilir grupları.</summary>
[Flags]
public enum BackupSettingsGroup
{
    None = 0,
    Institution = 1,
    Smtp = 2,
    Notifications = 4,
    WorkHours = 8,
    BackupAndDrive = 16,
    Appearance = 32,
    DesktopBehavior = 64,
    Connection = 128,

    All = Institution | Smtp | Notifications | WorkHours | BackupAndDrive
          | Appearance | DesktopBehavior | Connection
}

/// <summary>Yedeğe/geri yüklemeye seçilen bölümler ve ayar grupları.</summary>
public sealed record BackupSelection(BackupSection Sections, BackupSettingsGroup SettingsGroups)
{
    public static BackupSelection Full => new(BackupSection.All, BackupSettingsGroup.All);

    public static BackupSelection DataOnly => new(BackupSection.Data, BackupSettingsGroup.None);

    public bool Has(BackupSection section) => (Sections & section) == section;

    public bool HasSettings(BackupSettingsGroup group) => (SettingsGroups & group) == group;

    /// <summary>Randevular seçiliyse kişi ve kategoriler zorunludur; ayar grubu seçili
    /// değilse ayarlar bölümü yok sayılır.</summary>
    public BackupSelection Normalize()
    {
        var sections = Sections;

        if ((sections & BackupSection.Appointments) != 0)
            sections |= BackupSection.Persons | BackupSection.Categories;

        var groups = (sections & BackupSection.Settings) != 0
            ? SettingsGroups
            : BackupSettingsGroup.None;

        return new BackupSelection(sections, groups);
    }
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

/// <summary>Yedek oluşturma seçenekleri.</summary>
public sealed record BackupOptions(BackupSelection Selection)
{
    public static BackupOptions Full => new(BackupSelection.Full);
}

/// <summary>Randevuların mevcut verilerle nasıl birleştirileceği.</summary>
public enum AppointmentRestoreMode
{
    /// <summary>Aynı kimlikli kayıtlar daha yeni sürüm kazanacak şekilde birleştirilir.</summary>
    Merge = 0,

    /// <summary>Mevcut randevular silinip yedekteki randevular yazılır. Kullanıcı/ayarlar korunur.</summary>
    Replace = 1
}

/// <summary>Geri yükleme seçenekleri. Varsayılan: veri birleştir, kullanıcı ve ayarlara dokunma.</summary>
public sealed record RestoreOptions(
    BackupSelection Selection,
    AppointmentRestoreMode AppointmentMode = AppointmentRestoreMode.Merge,
    bool AllowEmptyBackup = false,
    bool IncludeMachineSpecificSettings = false)
{
    public static RestoreOptions Default => new(BackupSelection.DataOnly);
}

/// <summary>Bir yedeğin içeriğinin özeti; listede ve geri yükleme öncesinde gösterilir.</summary>
public sealed record BackupContentSummary(
    int UserCount,
    int PersonCount,
    int CategoryCount,
    int AppointmentCount,
    int ActiveAppointmentCount,
    int DeletedAppointmentCount,
    int OutcomeCount,
    int ReminderCount,
    int AuditLogCount,
    int SettingCount,
    int ShareLogCount,
    int ContentVersionCount,
    int RecipientCount,
    int BackupRecordCount,
    DateTime? FirstAppointmentLocal,
    DateTime? LastAppointmentLocal,
    DateTime? LastChangeLocal,
    bool HasInstitutionSettings,
    bool HasSmtpSettings,
    bool HasPushSettings,
    bool HasBackupSettings,
    bool HasDriveSettings,
    bool HasConnectionSettings,
    bool HasLogo)
{
    [JsonIgnore]
    public bool IsEmpty
        => UserCount == 0
           && PersonCount == 0
           && AppointmentCount == 0
           && SettingCount == 0;

    [JsonIgnore]
    public bool HasAppointments => AppointmentCount > 0;

    [JsonIgnore]
    public string CountsText
    {
        get
        {
            var parts = new List<string>
            {
                $"{AppointmentCount} randevu",
            };

            if (DeletedAppointmentCount > 0)
                parts.Add($"{DeletedAppointmentCount} silinmiş");

            parts.Add($"{PersonCount} kişi");
            parts.Add($"{UserCount} kullanıcı");
            parts.Add($"{CategoryCount} kategori");

            return string.Join(" · ", parts);
        }
    }

    [JsonIgnore]
    public string DatesText
    {
        get
        {
            var lastAppointment = LastAppointmentLocal is null
                ? "Son randevu yok"
                : $"Son randevu: {LastAppointmentLocal:dd.MM.yyyy}";

            var lastChange = LastChangeLocal is null
                ? "değişiklik bilgisi yok"
                : $"son değişiklik: {LastChangeLocal:dd.MM.yyyy HH:mm}";

            return $"{lastAppointment} · {lastChange}";
        }
    }

    [JsonIgnore]
    public string SettingsText
    {
        get
        {
            var parts = new List<string>();

            if (HasInstitutionSettings)
                parts.Add("kurum");
            if (HasSmtpSettings)
                parts.Add("SMTP");
            if (HasPushSettings)
                parts.Add("bildirim");
            if (HasBackupSettings)
                parts.Add("yedek");
            if (HasDriveSettings)
                parts.Add("Drive");
            if (HasConnectionSettings)
                parts.Add("sunucu bağlantısı");

            return parts.Count == 0 ? "ayar yok" : string.Join(" · ", parts);
        }
    }

    [JsonIgnore]
    public string CompactText
        => IsEmpty ? "Boş yedek" : $"{CountsText} · {SettingsText}";
}

/// <summary>Yedeği doğrulama ve içeriğini inceleme sonucu.</summary>
public sealed record BackupInspectionResult(
    bool Success,
    string? Error,
    BackupInfo? Backup,
    BackupManifest? Manifest,
    BackupContentSummary? Summary,
    BackupSelection AvailableSelection,
    IReadOnlyList<string> Warnings)
{
    public static BackupInspectionResult Ok(
        BackupInfo backup,
        BackupManifest manifest,
        BackupContentSummary summary,
        BackupSelection available,
        IReadOnlyList<string> warnings)
        => new(true, null, backup, manifest, summary, available, warnings);

    public static BackupInspectionResult Fail(string error)
        => new(false, error, null, null, null, BackupSelection.DataOnly, Array.Empty<string>());
}

/// <summary>Geri yükleme sonrası ne kadar verinin işlendiğini gösteren rapor.</summary>
public sealed record RestoreReport(
    int PersonsAdded,
    int PersonsUpdated,
    int CategoriesAdded,
    int CategoriesUpdated,
    int CategoriesMapped,
    int AppointmentsAdded,
    int AppointmentsUpdated,
    int AppointmentsSkipped,
    int OutcomesRestored,
    int UsersAdded,
    int UsersSkipped,
    int SettingsApplied,
    int SettingsSkipped,
    int AuditLogsAdded,
    int ShareLogsAdded,
    int ContentVersionsAdded,
    int RecipientsAdded,
    int BackupRecordsAdded,
    int HostsMapped,
    int RemindersSkipped,
    bool LogoRestored,
    IReadOnlyList<string> Warnings)
{
    [JsonIgnore]
    public string SummaryText
    {
        get
        {
            var parts = new List<string>();

            if (AppointmentsAdded > 0 || AppointmentsUpdated > 0)
                parts.Add($"{AppointmentsAdded} randevu eklendi, {AppointmentsUpdated} güncellendi");

            if (PersonsAdded > 0 || PersonsUpdated > 0)
                parts.Add($"{PersonsAdded} kişi eklendi, {PersonsUpdated} güncellendi");

            if (CategoriesAdded > 0 || CategoriesUpdated > 0)
                parts.Add($"{CategoriesAdded} kategori eklendi, {CategoriesUpdated} güncellendi");

            if (UsersAdded > 0)
                parts.Add($"{UsersAdded} kullanıcı eklendi");

            if (SettingsApplied > 0)
                parts.Add($"{SettingsApplied} ayar uygulandı");

            if (LogoRestored)
                parts.Add("kurum logosu geri yüklendi");

            return parts.Count == 0 ? "Geri yüklenecek yeni kayıt bulunamadı." : string.Join(" · ", parts);
        }
    }
}

public sealed record BackupCreateResult(
    bool Success,
    string? Error,
    BackupInfo? Backup)
{
    public static BackupCreateResult Ok(BackupInfo backup) => new(true, null, backup);
    public static BackupCreateResult Fail(string error) => new(false, error, null);
}

public sealed record BackupRestoreResult(
    bool Success,
    string? Error,
    RestoreReport? Report)
{
    public static BackupRestoreResult Ok(RestoreReport report) => new(true, null, report);
    public static BackupRestoreResult Fail(string error) => new(false, error, null);
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

/// <summary>Yedek manifesti. v2 alanları eski yedeklerde boş gelir.</summary>
public sealed record BackupManifest(
    string ProductName,
    string Version,
    int SchemaVersion,
    DateTime CreatedAtUtc,
    int UserCount,
    int PersonCount,
    int AppointmentCount,
    string DatabaseSha256,
    string SettingsSha256)
{
    public int ManifestVersion { get; init; } = 1;

    public BackupSelection Selection { get; init; } = BackupSelection.Full;

    public BackupContentSummary? Summary { get; init; }
}

public interface IBackupService
{
    /// <summary>Seçilen bölümleri içeren ZIP yedeği oluşturur.</summary>
    Task<BackupCreateResult> CreateAsync(
        BackupTrigger trigger,
        BackupOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Son yedekleri (en yeni önce) döndürür.</summary>
    Task<IReadOnlyList<BackupInfo>> GetHistoryAsync(int count = 50, CancellationToken cancellationToken = default);

    /// <summary>ZIP bütünlüğünü, manifesti ve SHA-256 özetini doğrular.</summary>
    Task<BackupValidationResult> ValidateAsync(string zipPath, CancellationToken cancellationToken = default);

    /// <summary>Yedeğin içeriğini (sayılar, tarihler, ayarlar) inceleyip seçilebilir bölümleri döndürür.</summary>
    Task<BackupInspectionResult> InspectAsync(string zipPath, CancellationToken cancellationToken = default);

    /// <summary>Seçilen bölümleri mevcut veritabanına birleştirir. Yalnızca yönetici çağırabilir.</summary>
    Task<BackupRestoreResult> RestoreAsync(
        string zipPath,
        RestoreOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Yedek ayar gruplarının anahtar eşlemesi.</summary>
public static class BackupSettingKeys
{
    public const string SyncBaseUrlKey = "sync.api.base_url";
    public const string SyncReadModeKey = "sync.read.enabled";

    private static readonly Dictionary<BackupSettingsGroup, string[]> Map = new()
    {
        [BackupSettingsGroup.Institution] = new[]
        {
            SettingsKeys.InstitutionName,
            SettingsKeys.InstitutionEmail,
            SettingsKeys.InstitutionPhone,
            SettingsKeys.InstitutionAddress,
            SettingsKeys.InstitutionLogoFile,
        },
        [BackupSettingsGroup.Smtp] = new[]
        {
            SettingsKeys.SmtpHost,
            SettingsKeys.SmtpPort,
            SettingsKeys.SmtpUseSsl,
            SettingsKeys.SmtpUsername,
            SettingsKeys.SmtpPassword,
            SettingsKeys.SmtpFromAddress,
            SettingsKeys.SmtpFromName,
        },
        [BackupSettingsGroup.Notifications] = new[]
        {
            SettingsKeys.NotificationsEnabled,
            SettingsKeys.DefaultReminderMinutes,
            SettingsKeys.PushEnabled,
            SettingsKeys.PushProvider,
            SettingsKeys.PushTopic,
            SettingsKeys.PushToken,
            SettingsKeys.PushChatId,
            SettingsKeys.QuietHoursEnabled,
            SettingsKeys.QuietHoursStart,
            SettingsKeys.QuietHoursEnd,
        },
        [BackupSettingsGroup.WorkHours] = new[]
        {
            SettingsKeys.WorkHoursStart,
            SettingsKeys.WorkHoursEnd,
            SettingsKeys.AllowOutOfHoursAppointments,
            SettingsKeys.DailyShareTime,
        },
        [BackupSettingsGroup.BackupAndDrive] = new[]
        {
            SettingsKeys.AutoBackupEnabled,
            SettingsKeys.BackupDirectory,
            SettingsKeys.BackupEncryptionEnabled,
            SettingsKeys.BackupPassword,
            SettingsKeys.GoogleDriveEnabled,
            SettingsKeys.GoogleClientId,
            SettingsKeys.GoogleClientSecret,
            SettingsKeys.GoogleRefreshToken,
            SettingsKeys.GoogleAccountEmail,
            SettingsKeys.GoogleFolderName,
            SettingsKeys.GoogleDriveFolder,
        },
        [BackupSettingsGroup.Appearance] = new[]
        {
            SettingsKeys.Theme,
        },
        [BackupSettingsGroup.DesktopBehavior] = new[]
        {
            SettingsKeys.AutoStartEnabled,
            SettingsKeys.AutoStartMode,
            SettingsKeys.CloseToTray,
        },
        [BackupSettingsGroup.Connection] = new[]
        {
            SyncBaseUrlKey,
            SyncReadModeKey,
        },
    };

    /// <summary>Bu bilgisayara özel; varsayılan geri yüklemede atlanan anahtarlar.</summary>
    public static IReadOnlyList<string> MachineSpecificKeys { get; } = new[]
    {
        SettingsKeys.BackupDirectory,
        SettingsKeys.BackupPassword,
        SettingsKeys.AutoStartEnabled,
        SettingsKeys.AutoStartMode,
        SettingsKeys.CloseToTray,
        SettingsKeys.GoogleDriveFolder,
        SettingsKeys.GoogleRefreshToken,
    };

    /// <summary>DPAPI ile şifrelenen; başka makinede çözülemeyebilecek anahtarlar.</summary>
    public static IReadOnlyList<string> SecretKeys { get; } = new[]
    {
        SettingsKeys.SmtpPassword,
        SettingsKeys.PushToken,
        SettingsKeys.PushChatId,
        SettingsKeys.BackupPassword,
        SettingsKeys.GoogleClientSecret,
        SettingsKeys.GoogleRefreshToken,
    };

    /// <summary>Hiçbir yedeğe yazılmayan sunucu oturumu anahtarları.</summary>
    public static IReadOnlyList<string> AlwaysExcludedKeys { get; } = new[]
    {
        "sync.api.access_token",
        "sync.api.refresh_token",
        "sync.api.expires_at",
        "sync.api.user",
    };

    public static IReadOnlyList<string> KeysFor(BackupSettingsGroup groups)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (group, values) in Map)
        {
            if ((groups & group) == 0)
                continue;

            foreach (var key in values)
                keys.Add(key);
        }

        return keys.ToList();
    }

    public static bool IsMachineSpecific(string key)
        => MachineSpecificKeys.Contains(key, StringComparer.Ordinal);

    public static bool IsSecret(string key)
        => SecretKeys.Contains(key, StringComparer.Ordinal);

    public static bool IsAlwaysExcluded(string key)
        => AlwaysExcludedKeys.Contains(key, StringComparer.Ordinal);
}
