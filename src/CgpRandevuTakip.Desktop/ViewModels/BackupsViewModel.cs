using System.Diagnostics;
using System.IO;
using System.Windows;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class BackupsViewModel : ObservableObject
{
    private readonly IBackupService _backups;
    private readonly ISettingsService _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IAppEnvironment _environment;
    private readonly IGoogleDriveFolderService _drive;

    public BackupsViewModel(
        IBackupService backups,
        ISettingsService settings,
        ICurrentUser currentUser,
        IDialogService dialogs,
        IAppEnvironment environment,
        IGoogleDriveFolderService drive)
    {
        _backups = backups;
        _settings = settings;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _environment = environment;
        _drive = drive;
    }

    public RangeObservableCollection<BackupInfo> Backups { get; } = new();

    [ObservableProperty]
    private BackupInfo? _selectedBackup;

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private string _summaryText = "Henüz yedek yok.";

    [ObservableProperty]
    private string _lastBackupText = "Henüz yedek alınmadı.";

    [ObservableProperty]
    private string _autoBackupText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    public bool HasBackups => Backups.Count > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var settings = await _settings.GetAsync();
            BackupDirectory = string.IsNullOrWhiteSpace(settings.BackupDirectory)
                ? _environment.DefaultBackupDirectory
                : settings.BackupDirectory;

            AutoBackupText = settings.AutoBackupEnabled
                ? "Otomatik yedekleme açık · günde 2 kez (12:30 ve öğleden sonra paylaşım sonrası, en geç 18:00)"
                : "Otomatik yedekleme kapalı.";

            var localRows = await _backups.GetHistoryAsync();
            var driveRows = await _drive.ListAsync();
            var rows = localRows
                .Concat(driveRows)
                .OrderByDescending(x => x.CreatedAtLocal)
                .ToList();

            Backups.ReplaceWith(rows);
            OnPropertyChanged(nameof(HasBackups));

            SummaryText = rows.Count == 0
                ? "Henüz yedek yok. İlk yedeği oluşturmak için \"Şimdi yedekle\" düğmesini kullanın."
                : $"{rows.Count} yedek · son 30 yedek saklanır";

            LastBackupText = rows.Count == 0
                ? "Henüz yedek alınmadı."
                : $"Son yedek: {rows[0].CreatedAtText} · {rows[0].SizeText}";

            if (SelectedBackup is not null)
                SelectedBackup = Backups.FirstOrDefault(x => x.Path == SelectedBackup.Path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yedekler yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _backups.CreateAsync(BackupTrigger.Manual);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            StatusMessage = $"Yedek oluşturuldu: {result.Backup!.FileName}";
            await LoadAsync();
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

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task ValidateSelectedAsync()
    {
        if (SelectedBackup is null)
            return;

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _backups.ValidateAsync(SelectedBackup.Path);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            StatusMessage =
                $"Yedek doğrulandı: {SelectedBackup.FileName} · " +
                $"{result.Manifest?.AppointmentCount ?? 0} görüşme · {result.Manifest?.Version}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yedek doğrulanamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedBackup is null)
            return;

        if (!_dialogs.Confirm(
                $"Seçili yedek geri yüklenecek:{Environment.NewLine}{SelectedBackup.FileName}{Environment.NewLine}{Environment.NewLine}" +
                "Mevcut veriler önce otomatik yedeklenecek, ardından uygulama yeniden başlayacak. Devam edilsin mi?",
                "Yedeği geri yükle"))
            return;

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var sourcePath = SelectedBackup.Path;
            if (SelectedBackup.IsDrive)
            {
                var download = await _drive.DownloadAsync(sourcePath);
                if (download.Path is null)
                {
                    ErrorMessage = download.Error;
                    return;
                }
                sourcePath = download.Path;
            }

            var validation = await _backups.ValidateAsync(sourcePath);
            if (!validation.Success)
            {
                ErrorMessage = validation.Error;
                return;
            }

            var result = await _backups.RestoreAsync(sourcePath);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            _dialogs.Info(
                "Yedek başarıyla geri yüklendi. Uygulama yeniden başlatılıyor.",
                "Geri yükleme tamamlandı");

            RestartApplication();
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

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{BackupDirectory}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Klasör açılamadı: {ex.Message}";
        }
    }

    private bool CanRestore()
        => IsAdmin && SelectedBackup is not null && !IsBusy;

    partial void OnSelectedBackupChanged(BackupInfo? value)
    {
        RestoreSelectedCommand.NotifyCanExecuteChanged();
        ValidateSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RestoreSelectedCommand.NotifyCanExecuteChanged();
        ValidateSelectedCommand.NotifyCanExecuteChanged();
    }

    private static void RestartApplication()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            Process.Start(new ProcessStartInfo(
                "cmd.exe",
                $"/c timeout /t 2 >nul & start \"\" \"{exe}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        System.Windows.Application.Current?.Shutdown();
    }
}
