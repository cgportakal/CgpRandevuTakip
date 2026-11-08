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
    string DailyShareTime)
{
    public static AppSettings CreateDefault(string defaultBackupDirectory) => new(
        AppTheme.Auto,
        string.Empty,
        defaultBackupDirectory,
        NotificationsEnabled: true,
        DefaultReminderMinutes: 30,
        PushEnabled: false,
        PushProvider: "ntfy",
        PushTopic: string.Empty,
        DailyShareTime: "08:30");
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
}
