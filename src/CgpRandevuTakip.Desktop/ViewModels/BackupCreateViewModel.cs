using CgpRandevuTakip.Application.Backups;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>Yedek alırken hangi bölümlerin dosyaya gireceğini seçtirir.</summary>
public partial class BackupCreateViewModel : ObservableObject
{
    private readonly IBackupService _backups;

    [ObservableProperty]
    private bool _includeAppointments = true;

    [ObservableProperty]
    private bool _includeUsers = true;

    [ObservableProperty]
    private bool _includeSettings = true;

    [ObservableProperty]
    private bool _includeAuditLogs = true;

    [ObservableProperty]
    private bool _includeShareLogs = true;

    [ObservableProperty]
    private bool _includeBackupHistory = true;

    [ObservableProperty]
    private bool _includeContentVersions = true;

    [ObservableProperty]
    private bool _includeRecipients = true;

    [ObservableProperty]
    private bool _includeLogo = true;

    [ObservableProperty]
    private bool _institutionSettings = true;

    [ObservableProperty]
    private bool _smtpSettings = true;

    [ObservableProperty]
    private bool _notificationSettings = true;

    [ObservableProperty]
    private bool _workHourSettings = true;

    [ObservableProperty]
    private bool _backupAndDriveSettings = true;

    [ObservableProperty]
    private bool _appearanceSettings = true;

    [ObservableProperty]
    private bool _desktopBehaviorSettings = true;

    [ObservableProperty]
    private bool _connectionSettings = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public BackupCreateViewModel(IBackupService backups)
    {
        _backups = backups;
    }

    public event EventHandler? Created;

    public string FullBackupNote
        => "Tam yedek: randevular, kişiler, kategoriler, kullanıcılar, ayarlar, denetim kaydı ve logo.";

    public bool HasSelection => Selection.Sections != BackupSection.None;

    public BackupSelection Selection => BuildSelection();

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy)
            return;

        var selection = Selection.Normalize();
        if (selection.Sections == BackupSection.None)
        {
            ErrorMessage = "En az bir bölüm seçin.";
            return;
        }

        if (selection.Has(BackupSection.Settings)
            && selection.SettingsGroups == BackupSettingsGroup.None)
        {
            ErrorMessage = "Ayarlar seçildi ancak hiçbir ayar grubu işaretlenmedi.";
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _backups.CreateAsync(
                BackupTrigger.Manual,
                new BackupOptions(selection));

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            StatusMessage = $"Yedek oluşturuldu: {result.Backup?.FileName}";
            OnPropertyChanged(nameof(Selection));
            Created?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yedek oluşturulamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIncludeAppointmentsChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeUsersChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeAuditLogsChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeShareLogsChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeBackupHistoryChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeContentVersionsChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeRecipientsChanged(bool value) => OnSelectionChanged();
    partial void OnIncludeLogoChanged(bool value) => OnSelectionChanged();

    partial void OnInstitutionSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnSmtpSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnNotificationSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnWorkHourSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnBackupAndDriveSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnAppearanceSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnDesktopBehaviorSettingsChanged(bool value) => OnSelectionChanged();
    partial void OnConnectionSettingsChanged(bool value) => OnSelectionChanged();

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(HasSelection));
        CreateCommand.NotifyCanExecuteChanged();
    }

    private BackupSelection BuildSelection()
    {
        var sections = BackupSection.None;

        if (IncludeAppointments)
            sections |= BackupSection.Appointments | BackupSection.Persons | BackupSection.Categories;
        if (IncludeUsers)
            sections |= BackupSection.Users;
        if (IncludeSettings)
            sections |= BackupSection.Settings;
        if (IncludeAuditLogs)
            sections |= BackupSection.AuditLogs;
        if (IncludeShareLogs)
            sections |= BackupSection.ShareLogs;
        if (IncludeBackupHistory)
            sections |= BackupSection.BackupHistory;
        if (IncludeContentVersions)
            sections |= BackupSection.ContentVersions;
        if (IncludeRecipients)
            sections |= BackupSection.Recipients;
        if (IncludeLogo)
            sections |= BackupSection.Logo;

        var groups = BackupSettingsGroup.None;
        if (IncludeSettings)
        {
            if (InstitutionSettings)
                groups |= BackupSettingsGroup.Institution;
            if (SmtpSettings)
                groups |= BackupSettingsGroup.Smtp;
            if (NotificationSettings)
                groups |= BackupSettingsGroup.Notifications;
            if (WorkHourSettings)
                groups |= BackupSettingsGroup.WorkHours;
            if (BackupAndDriveSettings)
                groups |= BackupSettingsGroup.BackupAndDrive;
            if (AppearanceSettings)
                groups |= BackupSettingsGroup.Appearance;
            if (DesktopBehaviorSettings)
                groups |= BackupSettingsGroup.DesktopBehavior;
            if (ConnectionSettings)
                groups |= BackupSettingsGroup.Connection;
        }

        return new BackupSelection(sections, groups);
    }
}
