namespace CgpRandevuTakip.Application.Settings;

public enum AppTheme
{
    Auto = 0,
    Light = 1,
    Dark = 2
}

/// <summary>Ayarlar ekranında yönetilen tüm değerler. Varsayılanlar tek yerde tanımlıdır.</summary>
public sealed record AppSettings(
    AppTheme Theme,
    string InstitutionName,
    string BackupDirectory,
    bool NotificationsEnabled,
    int DefaultReminderMinutes,
    bool PushEnabled,
    string PushProvider,
    string PushTopic,
    string DailyShareTime,
    string PushToken = "",
    string PushChatId = "",
    bool QuietHoursEnabled = true,
    string QuietHoursStart = "22:00",
    string QuietHoursEnd = "07:00",
    bool AutoStartEnabled = true,
    string AutoStartMode = "tray",
    bool CloseToTray = true,
    string SmtpHost = "",
    int SmtpPort = 587,
    bool SmtpUseSsl = true,
    string SmtpUsername = "",
    string SmtpPassword = "",
    string SmtpFromAddress = "",
    string SmtpFromName = "CGP - Randevu Takip",
    string WorkHoursStart = "06:00",
    string WorkHoursEnd = "20:45",
    bool AllowOutOfHoursAppointments = false,
    bool AutoBackupEnabled = true,
    bool BackupEncryptionEnabled = false,
    string BackupPassword = "",
    bool GoogleDriveEnabled = false,
    string GoogleClientId = "",
    string GoogleClientSecret = "",
    string GoogleRefreshToken = "",
    string GoogleAccountEmail = "",
    string GoogleFolderName = "CGP Yedekler",
    string GoogleDriveFolder = "",
    string InstitutionEmail = "",
    string InstitutionPhone = "",
    string InstitutionAddress = "",
    string InstitutionLogoFile = "")
{
    public static AppSettings CreateDefault(string defaultBackupDirectory) => new(
        AppTheme.Auto,
        "BEK Okulları",
        defaultBackupDirectory,
        NotificationsEnabled: true,
        DefaultReminderMinutes: 30,
        PushEnabled: false,
        PushProvider: "ntfy",
        PushTopic: string.Empty,
        DailyShareTime: "08:30",
        PushToken: string.Empty,
        PushChatId: string.Empty,
        QuietHoursEnabled: true,
        QuietHoursStart: "22:00",
        QuietHoursEnd: "07:00",
        AutoStartEnabled: true,
        AutoStartMode: "tray",
        CloseToTray: true,
        SmtpHost: string.Empty,
        SmtpPort: 587,
        SmtpUseSsl: true,
        SmtpUsername: string.Empty,
        SmtpPassword: string.Empty,
        SmtpFromAddress: string.Empty,
        SmtpFromName: "CGP - Randevu Takip",
        WorkHoursStart: "06:00",
        WorkHoursEnd: "20:45",
        AllowOutOfHoursAppointments: false,
        AutoBackupEnabled: true,
        BackupEncryptionEnabled: false,
        BackupPassword: string.Empty,
        GoogleDriveEnabled: false,
        GoogleClientId: string.Empty,
        GoogleClientSecret: string.Empty,
        GoogleRefreshToken: string.Empty,
        GoogleAccountEmail: string.Empty,
        GoogleFolderName: "CGP Yedekler",
        GoogleDriveFolder: string.Empty,
        InstitutionEmail: string.Empty,
        InstitutionPhone: string.Empty,
        InstitutionAddress: string.Empty,
        InstitutionLogoFile: string.Empty);
}

public static class SettingsKeys
{
    public const string Theme = "app.theme";
    public const string InstitutionName = "app.institution_name";
    public const string BackupDirectory = "app.backup_directory";
    public const string NotificationsEnabled = "app.notifications_enabled";
    public const string DefaultReminderMinutes = "app.default_reminder_minutes";
    public const string PushEnabled = "app.push_enabled";
    public const string PushProvider = "app.push_provider";
    public const string PushTopic = "app.push_topic";
    public const string DailyShareTime = "app.daily_share_time";
    public const string PushToken = "app.push_token";
    public const string PushChatId = "app.push_chat_id";
    public const string QuietHoursEnabled = "app.quiet_hours_enabled";
    public const string QuietHoursStart = "app.quiet_hours_start";
    public const string QuietHoursEnd = "app.quiet_hours_end";
    public const string AutoStartEnabled = "app.auto_start_enabled";
    public const string AutoStartMode = "app.auto_start_mode";
    public const string CloseToTray = "app.close_to_tray";
    public const string SmtpHost = "app.smtp_host";
    public const string SmtpPort = "app.smtp_port";
    public const string SmtpUseSsl = "app.smtp_use_ssl";
    public const string SmtpUsername = "app.smtp_username";
    public const string SmtpPassword = "app.smtp_password";
    public const string SmtpFromAddress = "app.smtp_from_address";
    public const string SmtpFromName = "app.smtp_from_name";
    public const string WorkHoursStart = "app.work_hours_start";
    public const string WorkHoursEnd = "app.work_hours_end";
    public const string AllowOutOfHoursAppointments = "app.allow_out_of_hours";
    public const string AutoBackupEnabled = "app.auto_backup_enabled";
    public const string BackupEncryptionEnabled = "app.backup_encryption_enabled";
    public const string BackupPassword = "app.backup_password";
    public const string GoogleDriveEnabled = "app.google_drive_enabled";
    public const string GoogleClientId = "app.google_client_id";
    public const string GoogleClientSecret = "app.google_client_secret";
    public const string GoogleRefreshToken = "app.google_refresh_token";
    public const string GoogleAccountEmail = "app.google_account_email";
    public const string GoogleFolderName = "app.google_folder_name";
    public const string GoogleDriveFolder = "app.google_drive_folder";
    public const string InstitutionEmail = "app.institution_email";
    public const string InstitutionPhone = "app.institution_phone";
    public const string InstitutionAddress = "app.institution_address";
    public const string InstitutionLogoFile = "app.institution_logo";
}
