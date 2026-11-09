using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Notifications;
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

public sealed record AutoStartModeOption(string Value, string Label);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeService _theme;
    private readonly IAppEnvironment _environment;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IAppointmentDialogService _shareDialogs;
    private readonly AutoStartService _autoStart;
    private readonly IPushNotifier _push;
    private readonly IEmailSender _email;
    private readonly WindowsToastService _windowsToast;
    private readonly IServiceProvider _services;

    private bool _isLoading;

    public SettingsViewModel(
        ISettingsService settings,
        ThemeService theme,
        IAppEnvironment environment,
        ICurrentUser currentUser,
        IDialogService dialogs,
        IAppointmentDialogService shareDialogs,
        AutoStartService autoStart,
        IPushNotifier push,
        IEmailSender email,
        WindowsToastService windowsToast,
        IServiceProvider services)
    {
        _settings = settings;
        _theme = theme;
        _environment = environment;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _shareDialogs = shareDialogs;
        _autoStart = autoStart;
        _push = push;
        _email = email;
        _windowsToast = windowsToast;
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

        AutoStartModeOptions = new ObservableCollection<AutoStartModeOption>
        {
            new("login", "Giriş ekranı açık"),
            new("tray", "Tepside gizli")
        };
    }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<ReminderOption> ReminderOptions { get; }
    public ObservableCollection<string> PushProviderOptions { get; }
    public ObservableCollection<AutoStartModeOption> AutoStartModeOptions { get; }

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
    private string _pushToken = string.Empty;

    [ObservableProperty]
    private string _pushChatId = string.Empty;

    [ObservableProperty]
    private bool _quietHoursEnabled = true;

    [ObservableProperty]
    private string _quietHoursStart = "22:00";

    [ObservableProperty]
    private string _quietHoursEnd = "07:00";

    [ObservableProperty]
    private string _smtpHost = string.Empty;

    [ObservableProperty]
    private string _smtpPort = "587";

    [ObservableProperty]
    private bool _smtpUseSsl = true;

    [ObservableProperty]
    private string _smtpUsername = string.Empty;

    [ObservableProperty]
    private string _smtpPassword = string.Empty;

    [ObservableProperty]
    private string _smtpFromAddress = string.Empty;

    [ObservableProperty]
    private string _smtpFromName = "CGP - Randevu Takip";

    [ObservableProperty]
    private string _testEmailAddress = string.Empty;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private AutoStartModeOption? _selectedAutoStartMode;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private string _dailyShareTime = "08:30";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    public bool IsTelegramProvider => SelectedPushProvider == "telegram";

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
            PushToken = settings.PushToken;
            PushChatId = settings.PushChatId;
            QuietHoursEnabled = settings.QuietHoursEnabled;
            QuietHoursStart = settings.QuietHoursStart;
            QuietHoursEnd = settings.QuietHoursEnd;
            SmtpHost = settings.SmtpHost;
            SmtpPort = settings.SmtpPort.ToString();
            SmtpUseSsl = settings.SmtpUseSsl;
            SmtpUsername = settings.SmtpUsername;
            SmtpPassword = settings.SmtpPassword;
            SmtpFromAddress = settings.SmtpFromAddress;
            SmtpFromName = settings.SmtpFromName;
            AutoStartEnabled = settings.AutoStartEnabled;
            CloseToTray = settings.CloseToTray;
            DailyShareTime = settings.DailyShareTime;
            SelectedAutoStartMode = AutoStartModeOptions.FirstOrDefault(x => x.Value == settings.AutoStartMode)
                                    ?? AutoStartModeOptions[0];
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
                DailyShareTime,
                PushToken,
                PushChatId,
                QuietHoursEnabled,
                QuietHoursStart,
                QuietHoursEnd,
                AutoStartEnabled,
                SelectedAutoStartMode?.Value ?? "login",
                CloseToTray,
                SmtpHost,
                int.TryParse(SmtpPort, out var smtpPort) ? smtpPort : 587,
                SmtpUseSsl,
                SmtpUsername,
                SmtpPassword,
                SmtpFromAddress,
                SmtpFromName);

            var result = await _settings.SaveAsync(settings);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            _theme.Apply(settings.Theme);
            _autoStart.SetEnabled(settings.AutoStartEnabled, settings.AutoStartMode);
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(settings.CloseToTray));

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
    private async Task OpenDailyShareAsync()
        => await _shareDialogs.ShowShareDailyListAsync(DateTime.Today);

    [RelayCommand]
    private async Task TestPushAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var sent = await _push.SendAsync(new PushRequest(
                SelectedPushProvider,
                PushTopic,
                PushToken,
                PushChatId,
                "CGP Test",
                $"Test bildirimi · {DateTime.Now:HH:mm}"));

            if (sent)
            {
                StatusMessage = "Test bildirimi gönderildi.";
                return;
            }

            ErrorMessage = SelectedPushProvider == "telegram"
                ? "Telegram testi başarısız. Bot token, sayısal chat id ve bota /start gönderildiğinden emin olun."
                : "ntfy testi başarısız. Topic adını ve internet bağlantısını kontrol edin.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Test bildirimi gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestEmailAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        var recipient = string.IsNullOrWhiteSpace(TestEmailAddress)
            ? SmtpFromAddress
            : TestEmailAddress;

        if (string.IsNullOrWhiteSpace(recipient))
        {
            ErrorMessage = "Test için alıcı e-posta adresi girin.";
            return;
        }

        IsBusy = true;
        try
        {
            var sent = await _email.SendAsync(
                recipient.Trim(),
                "CGP - Randevu Takip test e-postası",
                "Bu bir test e-postasıdır. SMTP ayarları çalışıyor.");

            if (sent.Success)
                StatusMessage = "Test e-postası gönderildi.";
            else
                ErrorMessage = sent.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void TestWindowsToast()
    {
        var shown = _windowsToast.TryShow(
            "CGP Test",
            $"Windows toast testi · {DateTime.Now:HH:mm}");

        if (shown)
            _dialogs.Info("Windows toast gönderildi. Bildirim merkezini kontrol edin.", "Toast testi");
        else
            _dialogs.Error(
                "Windows toast gönderilemedi. Uygulama içi bildirim kullanılmaya devam eder.",
                "Toast testi");
    }

    [RelayCommand]
    private void OpenDataFolder()
        => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_environment.DatabasePath}\"")
        {
            UseShellExecute = true
        });

    partial void OnSelectedPushProviderChanged(string value)
        => OnPropertyChanged(nameof(IsTelegramProvider));

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
