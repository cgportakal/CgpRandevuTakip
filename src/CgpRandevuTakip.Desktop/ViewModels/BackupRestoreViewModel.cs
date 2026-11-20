using CgpRandevuTakip.Application.Backups;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>
/// Yedek içeriğini gösterir ve hangi bölümlerin mevcut verilere birleştirileceğini
/// seçtirir. Kullanıcılar ve ayarlar varsayılan olarak kapalıdır.
/// </summary>
public partial class BackupRestoreViewModel : ObservableObject
{
    private readonly IBackupService _backups;

    [ObservableProperty]
    private BackupInfo? _backup;

    [ObservableProperty]
    private BackupInspectionResult? _inspection;

    [ObservableProperty]
    private bool _restoreAppointments;

    [ObservableProperty]
    private bool _restoreUsers;

    [ObservableProperty]
    private bool _restoreSettings;

    [ObservableProperty]
    private bool _restoreAuditLogs;

    [ObservableProperty]
    private bool _restoreShareLogs;

    [ObservableProperty]
    private bool _restoreBackupHistory;

    [ObservableProperty]
    private bool _restoreContentVersions;

    [ObservableProperty]
    private bool _restoreRecipients;

    [ObservableProperty]
    private bool _restoreLogo;

    [ObservableProperty]
    private bool _institutionSettings;

    [ObservableProperty]
    private bool _smtpSettings;

    [ObservableProperty]
    private bool _notificationSettings;

    [ObservableProperty]
    private bool _workHourSettings;

    [ObservableProperty]
    private bool _backupAndDriveSettings;

    [ObservableProperty]
    private bool _appearanceSettings;

    [ObservableProperty]
    private bool _desktopBehaviorSettings;

    [ObservableProperty]
    private bool _connectionSettings;

    [ObservableProperty]
    private int _appointmentModeIndex;

    [ObservableProperty]
    private bool _allowEmptyBackup;

    [ObservableProperty]
    private bool _includeMachineSpecificSettings;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _datesText = string.Empty;

    [ObservableProperty]
    private string _settingsText = string.Empty;

    [ObservableProperty]
    private string _warningsText = string.Empty;

    [ObservableProperty]
    private string _availabilityText = string.Empty;

    [ObservableProperty]
    private RestoreReport? _report;

    public BackupRestoreViewModel(IBackupService backups)
    {
        _backups = backups;
    }

    public event EventHandler? Restored;

    public string FileName => Backup?.FileName ?? string.Empty;

    public bool HasWarnings => !string.IsNullOrWhiteSpace(WarningsText);

    public bool IsEmptyBackup => Inspection?.Summary?.IsEmpty == true;

    public bool CanRestore
        => IsReady
           && !IsBusy
           && (!IsEmptyBackup || AllowEmptyBackup)
           && (IsEmptyBackup
               || Selection.Sections != BackupSection.None
               || Selection.SettingsGroups != BackupSettingsGroup.None)
           && (!RestoreSettings || Selection.SettingsGroups != BackupSettingsGroup.None);

    public BackupSelection Selection => BuildSelection();

    public async Task InitializeAsync(BackupInfo backup)
    {
        Backup = backup;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _backups.InspectAsync(backup.Path);
            Inspection = result;

            if (!result.Success || result.Summary is null)
            {
                ErrorMessage = result.Error ?? "Yedek incelenemedi.";
                WarningsText = string.Empty;
                return;
            }

            var summary = result.Summary;
            SummaryText = summary.CountsText;
            DatesText = summary.DatesText;
            SettingsText = summary.SettingsText;

            var available = result.AvailableSelection;
            RestoreAppointments = (available.Sections & BackupSection.Appointments) != 0;
            RestoreLogo = (available.Sections & BackupSection.Logo) != 0;
            AvailabilityText = BuildAvailabilityText(available);

            WarningsText = result.Warnings.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, result.Warnings.Select(x => "• " + x));

            IsReady = true;
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(IsEmptyBackup));
            NotifySelectionChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yedek incelenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        if (Backup is null || IsBusy)
            return;

        var selection = Selection.Normalize();
        if (selection.Sections == BackupSection.None
            && selection.SettingsGroups == BackupSettingsGroup.None
            && !(IsEmptyBackup && AllowEmptyBackup))
        {
            ErrorMessage = RestoreSettings
                ? "Ayarlar seçildi ancak hiçbir ayar grubu işaretlenmedi."
                : "Geri yüklenecek en az bir bölüm seçin.";
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var options = new RestoreOptions(
                selection,
                AppointmentModeIndex == 1 ? AppointmentRestoreMode.Replace : AppointmentRestoreMode.Merge,
                AllowEmptyBackup,
                IncludeMachineSpecificSettings);

            var result = await _backups.RestoreAsync(Backup.Path, options);
            if (!result.Success || result.Report is null)
            {
                ErrorMessage = result.Error ?? "Geri yükleme tamamlanamadı.";
                return;
            }

            Report = result.Report;
            StatusMessage = result.Report.SummaryText;
            Restored?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Geri yükleme başarısız: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnAllowEmptyBackupChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRestore));
        RestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRestore));
        RestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsReadyChanged(bool value) => NotifySelectionChanged();

    partial void OnRestoreAppointmentsChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreUsersChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreAuditLogsChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreShareLogsChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreBackupHistoryChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreContentVersionsChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreRecipientsChanged(bool value) => NotifySelectionChanged();
    partial void OnRestoreLogoChanged(bool value) => NotifySelectionChanged();

    partial void OnInstitutionSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnSmtpSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnNotificationSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnWorkHourSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnBackupAndDriveSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnAppearanceSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnDesktopBehaviorSettingsChanged(bool value) => NotifySelectionChanged();
    partial void OnConnectionSettingsChanged(bool value) => NotifySelectionChanged();

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(CanRestore));
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private BackupSelection BuildSelection()
    {
        var available = Inspection?.AvailableSelection.Sections ?? BackupSection.None;
        var sections = BackupSection.None;

        if (RestoreAppointments && (available & BackupSection.Appointments) != 0)
            sections |= BackupSection.Appointments | BackupSection.Persons | BackupSection.Categories;

        if (RestoreUsers && (available & BackupSection.Users) != 0)
            sections |= BackupSection.Users;

        if (RestoreSettings && (available & BackupSection.Settings) != 0)
            sections |= BackupSection.Settings;

        if (RestoreAuditLogs && (available & BackupSection.AuditLogs) != 0)
            sections |= BackupSection.AuditLogs;

        if (RestoreShareLogs && (available & BackupSection.ShareLogs) != 0)
            sections |= BackupSection.ShareLogs;

        if (RestoreBackupHistory && (available & BackupSection.BackupHistory) != 0)
            sections |= BackupSection.BackupHistory;

        if (RestoreContentVersions && (available & BackupSection.ContentVersions) != 0)
            sections |= BackupSection.ContentVersions;

        if (RestoreRecipients && (available & BackupSection.Recipients) != 0)
            sections |= BackupSection.Recipients;

        if (RestoreLogo && (available & BackupSection.Logo) != 0)
            sections |= BackupSection.Logo;

        var groups = BackupSettingsGroup.None;
        if (RestoreSettings)
        {
            var availableGroups = Inspection?.AvailableSelection.SettingsGroups ?? BackupSettingsGroup.None;

            if (InstitutionSettings && (availableGroups & BackupSettingsGroup.Institution) != 0)
                groups |= BackupSettingsGroup.Institution;
            if (SmtpSettings && (availableGroups & BackupSettingsGroup.Smtp) != 0)
                groups |= BackupSettingsGroup.Smtp;
            if (NotificationSettings && (availableGroups & BackupSettingsGroup.Notifications) != 0)
                groups |= BackupSettingsGroup.Notifications;
            if (WorkHourSettings && (availableGroups & BackupSettingsGroup.WorkHours) != 0)
                groups |= BackupSettingsGroup.WorkHours;
            if (BackupAndDriveSettings && (availableGroups & BackupSettingsGroup.BackupAndDrive) != 0)
                groups |= BackupSettingsGroup.BackupAndDrive;
            if (AppearanceSettings && (availableGroups & BackupSettingsGroup.Appearance) != 0)
                groups |= BackupSettingsGroup.Appearance;
            if (DesktopBehaviorSettings && (availableGroups & BackupSettingsGroup.DesktopBehavior) != 0)
                groups |= BackupSettingsGroup.DesktopBehavior;
            if (ConnectionSettings && (availableGroups & BackupSettingsGroup.Connection) != 0)
                groups |= BackupSettingsGroup.Connection;
        }

        return new BackupSelection(sections, groups);
    }

    private static string BuildAvailabilityText(BackupSelection available)
    {
        var parts = new List<string>();

        if ((available.Sections & BackupSection.Appointments) != 0)
            parts.Add("randevular");
        if ((available.Sections & BackupSection.Users) != 0)
            parts.Add("kullanıcılar");
        if ((available.Sections & BackupSection.Settings) != 0)
            parts.Add("ayarlar");
        if ((available.Sections & BackupSection.AuditLogs) != 0)
            parts.Add("denetim kaydı");
        if ((available.Sections & BackupSection.ShareLogs) != 0)
            parts.Add("paylaşım geçmişi");
        if ((available.Sections & BackupSection.BackupHistory) != 0)
            parts.Add("yedek geçmişi");
        if ((available.Sections & BackupSection.ContentVersions) != 0)
            parts.Add("içerik sürümleri");
        if ((available.Sections & BackupSection.Recipients) != 0)
            parts.Add("alıcılar");
        if ((available.Sections & BackupSection.Logo) != 0)
            parts.Add("kurum logosu");

        return parts.Count == 0
            ? "Bu yedekte geri yüklenebilecek veri bulunamadı."
            : "Geri yüklenebilir: " + string.Join(" · ", parts);
    }
}
