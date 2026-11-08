using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record ThemeOption(AppTheme Value, string Label);

public sealed record ReminderOption(int Minutes, string Label);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeService _theme;
    private readonly IAppEnvironment _environment;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly ISpikeService _spike;
    private readonly IServiceProvider _services;

    private bool _isLoading;

    public SettingsViewModel(
        ISettingsService settings,
        ThemeService theme,
        IAppEnvironment environment,
        ICurrentUser currentUser,
        IDialogService dialogs,
        ISpikeService spike,
        IServiceProvider services)
    {
        _settings = settings;
        _theme = theme;
        _environment = environment;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _spike = spike;
        _services = services;

        ThemeOptions = new ObservableCollection<ThemeOption>
        {
            new(AppTheme.Auto, "Otomatik (Windows teması)"),
            new(AppTheme.Light, "Açık"),
            new(AppTheme.Dark, "Koyu")
        };

        ReminderOptions = new ObservableCollection<ReminderOption>
        {
            new(30, "30 dakika önce"),
            new(60, "1 saat önce"),
            new(1440, "1 gün önce")
        };

        PushProviderOptions = new ObservableCollection<string> { "ntfy", "telegram" };
    }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<ReminderOption> ReminderOptions { get; }
    public ObservableCollection<string> PushProviderOptions { get; }

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    [ObservableProperty]
    private ReminderOption? _selectedReminder;

    [ObservableProperty]
    private string _institutionName = string.Empty;

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private bool _pushEnabled;

    [ObservableProperty]
    private string _selectedPushProvider = "ntfy";

    [ObservableProperty]
    private string _pushTopic = string.Empty;

    [ObservableProperty]
    private string _dailyShareTime = "08:30";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    public string DatabasePath => _environment.DatabasePath;

    public string LogsDirectory => _environment.LogsDirectory;

    public string VersionText => $"Sürüm {AppConstants.Version}";

    public string Copyright => AppConstants.Copyright;

    public string ProductName => AppConstants.ProductName;

    [RelayCommand]
    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var settings = await _settings.GetAsync();

            InstitutionName = settings.InstitutionName;
            BackupDirectory = settings.BackupDirectory;
            NotificationsEnabled = settings.NotificationsEnabled;
            PushEnabled = settings.PushEnabled;
            SelectedPushProvider = settings.PushProvider;
            PushTopic = settings.PushTopic;
            DailyShareTime = settings.DailyShareTime;
            SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Value == settings.Theme) ?? ThemeOptions[0];
            SelectedReminder = ReminderOptions.FirstOrDefault(x => x.Minutes == settings.DefaultReminderMinutes)
                               ?? ReminderOptions[0];
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsAdmin)
        {
            ErrorMessage = "Ayarları yalnızca yönetici değiştirebilir.";
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var settings = new AppSettings(
                SelectedTheme?.Value ?? AppTheme.Auto,
                InstitutionName,
                BackupDirectory,
                NotificationsEnabled,
                SelectedReminder?.Minutes ?? 30,
                PushEnabled,
                SelectedPushProvider,
                PushTopic,
                DailyShareTime);

            var result = await _settings.SaveAsync(settings);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            _theme.Apply(settings.Theme);

            var institution = AppConstants.ResolveInstitutionName(settings.InstitutionName);
            InstitutionName = institution;
            WeakReferenceMessenger.Default.Send(new InstitutionChangedMessage(institution));

            StatusMessage = "Ayarlar kaydedildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ayarlar kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BrowseBackupDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Yedek klasörünü seçin"
        };

        if (Directory.Exists(BackupDirectory))
            dialog.InitialDirectory = BackupDirectory;

        if (dialog.ShowDialog() == true)
            BackupDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void ChangePassword()
    {
        var window = _services.GetRequiredService<Views.ChangePasswordWindow>();
        window.Owner = System.Windows.Application.Current?.MainWindow;
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task PrepareDailyListAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        var result = await _spike.PrepareDailyListImageAsync();
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        StatusMessage = result.Message;

        if (_dialogs.Confirm(
                "Günlük liste görseli panoya kopyalandı.\n\nWhatsApp şimdi açılsın mı? Açıldıktan sonra sohbete yapıştırın (Ctrl+V).",
                "S-01 · WhatsApp panosu"))
        {
            var open = _spike.OpenWhatsApp();
            if (open.Success)
                _dialogs.Info(open.Message, "S-01 · WhatsApp");
            else
                _dialogs.Error(open.Message, "S-01 · WhatsApp açılamadı");
        }
    }

    [RelayCommand]
    private void ShowReminderPreview() => _spike.ShowReminderPreview();

    [RelayCommand]
    private void ShowToastPreview() => _spike.ShowToastPreview();

    [RelayCommand]
    private void OpenDataFolder()
        => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_environment.DatabasePath}\"")
        {
            UseShellExecute = true
        });

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_isLoading || value is null)
            return;

        _ = ApplyThemeAsync(value.Value);
    }

    private async Task ApplyThemeAsync(AppTheme theme)
    {
        try
        {
            var result = await _settings.SetThemeAsync(theme);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            _theme.Apply(theme);
            StatusMessage = "Tema uygulandı.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Tema uygulanamadı: {ex.Message}";
        }
    }
}
