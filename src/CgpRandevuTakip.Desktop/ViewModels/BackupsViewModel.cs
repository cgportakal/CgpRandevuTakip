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
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class BackupsViewModel : ObservableObject
{
    private readonly IBackupService _backups;
    private readonly ISettingsService _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IAppEnvironment _environment;
    private readonly IGoogleDriveFolderService _drive;
    private readonly IServiceProvider _services;

    public BackupsViewModel(
        IBackupService backups,
        ISettingsService settings,
        ICurrentUser currentUser,
        IDialogService dialogs,
        IAppEnvironment environment,
        IGoogleDriveFolderService drive,
        IServiceProvider services)
    {
        _backups = backups;
        _settings = settings;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _environment = environment;
        _drive = drive;
        _services = services;
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

    [ObservableProperty]
    private string _inspectionSummaryText = string.Empty;

    [ObservableProperty]
    private string _inspectionDatesText = string.Empty;

    [ObservableProperty]
    private string _inspectionSettingsText = string.Empty;

    [ObservableProperty]
    private string _inspectionWarningsText = string.Empty;

    [ObservableProperty]
    private bool _hasInspection;

    [ObservableProperty]
    private bool _isInspecting;

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

            if (SelectedBackup is null && Backups.Count > 0)
                SelectedBackup = Backups[0];
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

        var viewModel = _services.GetRequiredService<BackupCreateViewModel>();
        var window = new Views.BackupCreateWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
            return;

        StatusMessage = "Yedek oluşturuldu.";
        await LoadAsync();
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

        ErrorMessage = null;
        StatusMessage = null;

        var sourcePath = SelectedBackup.Path;
        if (SelectedBackup.IsDrive)
        {
            IsBusy = true;
            try
            {
                var download = await _drive.DownloadAsync(sourcePath);
                if (download.Path is null)
                {
                    ErrorMessage = download.Error;
                    return;
                }

                sourcePath = download.Path;
            }
            finally
            {
                IsBusy = false;
            }
        }

        var backup = SelectedBackup with { Path = sourcePath };
        var viewModel = _services.GetRequiredService<BackupRestoreViewModel>();

        await viewModel.InitializeAsync(backup);
        if (!viewModel.IsReady)
        {
            ErrorMessage = viewModel.ErrorMessage ?? "Yedek incelenemedi.";
            return;
        }

        var window = new Views.BackupRestoreWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
            return;

        var report = viewModel.Report;
        StatusMessage = report?.SummaryText ?? "Yedek geri yüklendi.";
        await LoadAsync();

        var warnings = report is { Warnings.Count: > 0 }
            ? Environment.NewLine + Environment.NewLine + "Uyarılar:" + Environment.NewLine +
              string.Join(Environment.NewLine, report.Warnings.Select(x => "• " + x))
            : string.Empty;

        _dialogs.Info(
            "Yedek geri yüklendi." + Environment.NewLine + Environment.NewLine +
            (report?.SummaryText ?? string.Empty) + warnings,
            "Geri yükleme tamamlandı");

        if (_dialogs.Confirm(
                "Değişikliklerin tam uygulanması için uygulama yeniden başlatılsın mı? " +
                "Mevcut oturumunuz ve kullanıcılarınız korunur.",
                "Yeniden başlat"))
        {
            RestartApplication();
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
        _ = InspectSelectedAsync(value);
    }

    private async Task InspectSelectedAsync(BackupInfo? backup)
    {
        ErrorMessage = null;

        if (backup is null)
        {
            HasInspection = false;
            InspectionSummaryText = string.Empty;
            InspectionDatesText = string.Empty;
            InspectionSettingsText = string.Empty;
            InspectionWarningsText = string.Empty;
            return;
        }

        IsInspecting = true;
        try
        {
            var result = await _backups.InspectAsync(backup.Path);
            if (!result.Success || result.Summary is null)
            {
                HasInspection = false;
                ErrorMessage = result.Error ?? "Yedek incelenemedi.";
                return;
            }

            InspectionSummaryText = result.Summary.CountsText;
            InspectionDatesText = result.Summary.DatesText;
            InspectionSettingsText = result.Summary.SettingsText;
            InspectionWarningsText = result.Warnings.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, result.Warnings.Select(x => "• " + x));
            HasInspection = true;
        }
        catch (Exception ex)
        {
            HasInspection = false;
            ErrorMessage = $"Yedek incelenemedi: {ex.Message}";
        }
        finally
        {
            IsInspecting = false;
        }
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
