using System.Globalization;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly int[] ReminderValues = { 30, 60, 1440 };
    private static readonly string[] PushProviders = { "ntfy", "telegram" };

    private readonly ISettingsRepository _repository;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IAppEnvironment _environment;
    private readonly ISecretProtector _secrets;

    private AppSettings? _cachedSettings;

    public SettingsService(
        ISettingsRepository repository,
        IAuditLogger audit,
        ICurrentUser currentUser,
        IAppEnvironment environment,
        ISecretProtector secrets)
    {
        _repository = repository;
        _audit = audit;
        _currentUser = currentUser;
        _environment = environment;
        _secrets = secrets;
    }

    public IReadOnlyList<int> AllowedReminderMinutes => ReminderValues;

    public async Task<AppSettings> GetAsync()
    {
        // Ayarlar küçük ve seyrek değişen bir tablodur; tek örnekli servis içinde
        // önbelleklenir. Yazma işlemleri önbelleği geçersiz kılar.
        if (_cachedSettings is not null)
            return _cachedSettings;

        var values = await _repository.GetAllAsync();
        var defaults = AppSettings.CreateDefault(_environment.DefaultBackupDirectory);

        var settings = new AppSettings(
            Theme: ParseTheme(Read(values, SettingsKeys.Theme)),
            InstitutionName: Read(values, SettingsKeys.InstitutionName) ?? defaults.InstitutionName,
            BackupDirectory: NormalizeDirectory(Read(values, SettingsKeys.BackupDirectory)) ?? defaults.BackupDirectory,
            NotificationsEnabled: ReadBool(values, SettingsKeys.NotificationsEnabled, defaults.NotificationsEnabled),
            DefaultReminderMinutes: ReadReminder(values, defaults.DefaultReminderMinutes),
            PushEnabled: ReadBool(values, SettingsKeys.PushEnabled, defaults.PushEnabled),
            PushProvider: NormalizeProvider(Read(values, SettingsKeys.PushProvider)),
            PushTopic: Read(values, SettingsKeys.PushTopic) ?? defaults.PushTopic,
            DailyShareTime: ReadTime(values, SettingsKeys.DailyShareTime, defaults.DailyShareTime),
            PushToken: _secrets.Unprotect(Read(values, SettingsKeys.PushToken)) ?? defaults.PushToken,
            PushChatId: _secrets.Unprotect(Read(values, SettingsKeys.PushChatId)) ?? defaults.PushChatId,
            QuietHoursEnabled: ReadBool(values, SettingsKeys.QuietHoursEnabled, defaults.QuietHoursEnabled),
            QuietHoursStart: ReadTime(values, SettingsKeys.QuietHoursStart, defaults.QuietHoursStart),
            QuietHoursEnd: ReadTime(values, SettingsKeys.QuietHoursEnd, defaults.QuietHoursEnd),
            AutoStartEnabled: ReadBool(values, SettingsKeys.AutoStartEnabled, defaults.AutoStartEnabled),
            AutoStartMode: NormalizeAutoStartMode(
                Read(values, SettingsKeys.AutoStartMode) ?? defaults.AutoStartMode),
            CloseToTray: ReadBool(values, SettingsKeys.CloseToTray, defaults.CloseToTray),
            SmtpHost: Read(values, SettingsKeys.SmtpHost) ?? defaults.SmtpHost,
            SmtpPort: ReadInt(values, SettingsKeys.SmtpPort, defaults.SmtpPort),
            SmtpUseSsl: ReadBool(values, SettingsKeys.SmtpUseSsl, defaults.SmtpUseSsl),
            SmtpUsername: Read(values, SettingsKeys.SmtpUsername) ?? defaults.SmtpUsername,
            SmtpPassword: _secrets.Unprotect(Read(values, SettingsKeys.SmtpPassword)) ?? defaults.SmtpPassword,
            SmtpFromAddress: Read(values, SettingsKeys.SmtpFromAddress) ?? defaults.SmtpFromAddress,
            SmtpFromName: Read(values, SettingsKeys.SmtpFromName) ?? defaults.SmtpFromName,
            WorkHoursStart: ReadTime(values, SettingsKeys.WorkHoursStart, defaults.WorkHoursStart),
            WorkHoursEnd: ReadTime(values, SettingsKeys.WorkHoursEnd, defaults.WorkHoursEnd),
            AllowOutOfHoursAppointments: ReadBool(
                values,
                SettingsKeys.AllowOutOfHoursAppointments,
                defaults.AllowOutOfHoursAppointments),
            AutoBackupEnabled: ReadBool(
                values,
                SettingsKeys.AutoBackupEnabled,
                defaults.AutoBackupEnabled),
            BackupEncryptionEnabled: ReadBool(
                values,
                SettingsKeys.BackupEncryptionEnabled,
                defaults.BackupEncryptionEnabled),
            BackupPassword: _secrets.Unprotect(Read(values, SettingsKeys.BackupPassword))
                            ?? defaults.BackupPassword,
            GoogleDriveEnabled: ReadBool(
                values,
                SettingsKeys.GoogleDriveEnabled,
                defaults.GoogleDriveEnabled),
            GoogleClientId: Read(values, SettingsKeys.GoogleClientId) ?? defaults.GoogleClientId,
            GoogleClientSecret: _secrets.Unprotect(Read(values, SettingsKeys.GoogleClientSecret))
                                ?? defaults.GoogleClientSecret,
            GoogleRefreshToken: _secrets.Unprotect(Read(values, SettingsKeys.GoogleRefreshToken))
                                ?? defaults.GoogleRefreshToken,
            GoogleAccountEmail: Read(values, SettingsKeys.GoogleAccountEmail) ?? defaults.GoogleAccountEmail,
            GoogleFolderName: Read(values, SettingsKeys.GoogleFolderName) ?? defaults.GoogleFolderName,
            GoogleDriveFolder: Read(values, SettingsKeys.GoogleDriveFolder) ?? defaults.GoogleDriveFolder);

        _cachedSettings = settings;
        return settings;
    }

    public async Task<OperationResult> SaveAsync(AppSettings settings)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return OperationResult.Fail("Ayarları yalnızca yönetici değiştirebilir.");

        var institution = settings.InstitutionName?.Trim() ?? string.Empty;
        if (institution.Length > 120)
            return OperationResult.Fail("Kurum adı en fazla 120 karakter olabilir.");

        var backupDirectory = NormalizeDirectory(settings.BackupDirectory) ?? _environment.DefaultBackupDirectory;
        var writeTest = TestDirectory(backupDirectory);
        if (writeTest is not null)
            return OperationResult.Fail(writeTest);

        if (!ReminderValues.Contains(settings.DefaultReminderMinutes))
            return OperationResult.Fail("Varsayılan hatırlatıcı 30 dk, 1 saat veya 1 gün olmalıdır.");

        if (!TimeSpan.TryParseExact(settings.DailyShareTime, @"hh\:mm", CultureInfo.InvariantCulture, out _))
            return OperationResult.Fail("Günlük paylaşım saati SS:dd biçiminde olmalıdır.");

        if (!TimeSpan.TryParseExact(settings.QuietHoursStart, @"hh\:mm", CultureInfo.InvariantCulture, out _)
            || !TimeSpan.TryParseExact(settings.QuietHoursEnd, @"hh\:mm", CultureInfo.InvariantCulture, out _))
        {
            return OperationResult.Fail("Sessiz saatler SS:dd biçiminde olmalıdır.");
        }

        if (!TimeSpan.TryParseExact(settings.WorkHoursStart, @"hh\:mm", CultureInfo.InvariantCulture, out var workStart)
            || !TimeSpan.TryParseExact(settings.WorkHoursEnd, @"hh\:mm", CultureInfo.InvariantCulture, out var workEnd))
        {
            return OperationResult.Fail("Çalışma saatleri SS:dd biçiminde olmalıdır.");
        }

        if (workStart >= workEnd)
            return OperationResult.Fail("Çalışma saati başlangıcı bitişten önce olmalıdır.");

        var backupPassword = settings.BackupPassword ?? string.Empty;
        if (backupPassword.Length is > 0 and < 8)
            return OperationResult.Fail("Yedek parolası en az 8 karakter olmalıdır.");

        if (settings.BackupEncryptionEnabled && string.IsNullOrWhiteSpace(backupPassword))
            return OperationResult.Fail("Yedek şifreleme açıkken parola zorunludur.");

        if (settings.GoogleDriveEnabled)
        {
            if (string.IsNullOrWhiteSpace(settings.GoogleDriveFolder))
                return OperationResult.Fail("Google Drive klasörü seçilmelidir.");

            var driveFolder = settings.GoogleDriveFolder.Trim();
            if (!Directory.Exists(driveFolder))
                return OperationResult.Fail("Google Drive klasörü bulunamadı. Drive for Desktop senkronizasyonunu kontrol edin.");

            if (!settings.BackupEncryptionEnabled)
                return OperationResult.Fail("Google Drive'a yükleme için yedek şifreleme açık olmalıdır.");
        }

        if (settings.SmtpPort is < 1 or > 65535)
            return OperationResult.Fail("SMTP portu 1–65535 arasında olmalıdır.");

        if (!string.IsNullOrWhiteSpace(settings.SmtpFromAddress)
            && !EmailAddressPolicy.TryNormalize(settings.SmtpFromAddress, out _))
        {
            return OperationResult.Fail("SMTP gönderen adresi geçerli değil.");
        }

        if (!string.IsNullOrWhiteSpace(settings.SmtpHost)
            && string.IsNullOrWhiteSpace(settings.SmtpFromAddress))
        {
            return OperationResult.Fail("SMTP kullanırken gönderen adresi zorunludur.");
        }

        var provider = NormalizeProvider(settings.PushProvider);
        var autoStartMode = NormalizeAutoStartMode(settings.AutoStartMode);
        var previous = await GetAsync();

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SettingsKeys.Theme] = settings.Theme.ToString(),
            [SettingsKeys.InstitutionName] = institution,
            [SettingsKeys.BackupDirectory] = backupDirectory,
            [SettingsKeys.NotificationsEnabled] = settings.NotificationsEnabled.ToString(),
            [SettingsKeys.DefaultReminderMinutes] = settings.DefaultReminderMinutes.ToString(CultureInfo.InvariantCulture),
            [SettingsKeys.PushEnabled] = settings.PushEnabled.ToString(),
            [SettingsKeys.PushProvider] = provider,
            [SettingsKeys.PushTopic] = settings.PushTopic?.Trim() ?? string.Empty,
            [SettingsKeys.DailyShareTime] = settings.DailyShareTime,
            [SettingsKeys.PushToken] = _secrets.Protect(settings.PushToken?.Trim()) ?? string.Empty,
            [SettingsKeys.PushChatId] = _secrets.Protect(settings.PushChatId?.Trim()) ?? string.Empty,
            [SettingsKeys.QuietHoursEnabled] = settings.QuietHoursEnabled.ToString(),
            [SettingsKeys.QuietHoursStart] = settings.QuietHoursStart,
            [SettingsKeys.QuietHoursEnd] = settings.QuietHoursEnd,
            [SettingsKeys.AutoStartEnabled] = settings.AutoStartEnabled.ToString(),
            [SettingsKeys.AutoStartMode] = autoStartMode,
            [SettingsKeys.CloseToTray] = settings.CloseToTray.ToString(),
            [SettingsKeys.SmtpHost] = settings.SmtpHost?.Trim() ?? string.Empty,
            [SettingsKeys.SmtpPort] = settings.SmtpPort.ToString(CultureInfo.InvariantCulture),
            [SettingsKeys.SmtpUseSsl] = settings.SmtpUseSsl.ToString(),
            [SettingsKeys.SmtpUsername] = settings.SmtpUsername?.Trim() ?? string.Empty,
            [SettingsKeys.SmtpPassword] = _secrets.Protect(settings.SmtpPassword) ?? string.Empty,
            [SettingsKeys.SmtpFromAddress] = settings.SmtpFromAddress?.Trim() ?? string.Empty,
            [SettingsKeys.SmtpFromName] = settings.SmtpFromName?.Trim() ?? string.Empty,
            [SettingsKeys.WorkHoursStart] = settings.WorkHoursStart,
            [SettingsKeys.WorkHoursEnd] = settings.WorkHoursEnd,
            [SettingsKeys.AllowOutOfHoursAppointments] = settings.AllowOutOfHoursAppointments.ToString(),
            [SettingsKeys.AutoBackupEnabled] = settings.AutoBackupEnabled.ToString(),
            [SettingsKeys.BackupEncryptionEnabled] = settings.BackupEncryptionEnabled.ToString(),
            [SettingsKeys.BackupPassword] = _secrets.Protect(settings.BackupPassword ?? string.Empty) ?? string.Empty,
            [SettingsKeys.GoogleDriveEnabled] = settings.GoogleDriveEnabled.ToString(),
            [SettingsKeys.GoogleClientId] = settings.GoogleClientId?.Trim() ?? string.Empty,
            [SettingsKeys.GoogleClientSecret] = _secrets.Protect(settings.GoogleClientSecret) ?? string.Empty,
            [SettingsKeys.GoogleRefreshToken] = _secrets.Protect(settings.GoogleRefreshToken) ?? string.Empty,
            [SettingsKeys.GoogleAccountEmail] = settings.GoogleAccountEmail?.Trim() ?? string.Empty,
            [SettingsKeys.GoogleFolderName] = string.IsNullOrWhiteSpace(settings.GoogleFolderName)
                ? "CGP Yedekler"
                : settings.GoogleFolderName.Trim(),
            [SettingsKeys.GoogleDriveFolder] = settings.GoogleDriveFolder?.Trim() ?? string.Empty
        };

        await _repository.SetManyAsync(values);
        _cachedSettings = null;

        var action = previous.Theme != settings.Theme
            ? AuditAction.ThemeChanged
            : AuditAction.SettingsChanged;

        await _audit.LogAsync(action, _currentUser.UserId);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetThemeAsync(AppTheme theme)
    {
        if (!_currentUser.IsAuthenticated)
            return OperationResult.Fail("Oturum bulunamadı. Lütfen yeniden giriş yapın.");

        if (!Enum.IsDefined(theme))
            return OperationResult.Fail("Geçersiz tema seçimi.");

        var previous = await GetAsync();
        if (previous.Theme == theme)
            return OperationResult.Ok();

        await _repository.SetManyAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SettingsKeys.Theme] = theme.ToString()
        });
        _cachedSettings = null;

        await _audit.LogAsync(AuditAction.ThemeChanged, _currentUser.UserId);

        return OperationResult.Ok();
    }

    private static string? Read(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool fallback)
        => values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private static int ReadInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback)
        => values.TryGetValue(key, out var value)
           && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static int ReadReminder(IReadOnlyDictionary<string, string> values, int fallback)
        => values.TryGetValue(SettingsKeys.DefaultReminderMinutes, out var value)
           && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
           && ReminderValues.Contains(parsed)
            ? parsed
            : fallback;

    private static string ReadTime(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback)
    {
        var value = Read(values, key);
        return value is not null
               && TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out _)
            ? value
            : fallback;
    }

    private static AppTheme ParseTheme(string? value)
        => Enum.TryParse<AppTheme>(value, ignoreCase: true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : AppTheme.Auto;

    private static string NormalizeProvider(string? provider)
    {
        var normalized = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return PushProviders.Contains(normalized) ? normalized : "ntfy";
    }

    private static string NormalizeAutoStartMode(string? mode)
        => (mode ?? string.Empty).Trim().ToLowerInvariant() == "tray" ? "tray" : "login";

    private static string? NormalizeDirectory(string? directory)
        => string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();

    private static string? TestDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".cgp-yazma-testi-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return null;
        }
        catch (Exception ex)
        {
            return $"Yedek klasörüne yazılamıyor: {ex.Message}";
        }
    }
}
