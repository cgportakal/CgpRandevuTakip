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
    bool AutoStartEnabled = false,
    string AutoStartMode = "login",
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
    bool AutoBackupEnabled = true)
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
        AutoStartEnabled: false,
        AutoStartMode: "login",
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
        AutoBackupEnabled: true);
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
    public const string FirstRunCompleted = "app.first_run_completed";
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
}
