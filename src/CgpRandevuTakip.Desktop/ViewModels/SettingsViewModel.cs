using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Notifications;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
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
    private readonly IBitLockerService _bitLocker;
    private readonly IGoogleDriveAuthService _googleAuth;
    private readonly IUpdateService _updates;
    private UpdateCheckResult? _lastUpdate;
    private string _googleRefreshToken = string.Empty;
    private string _googleAccountEmail = string.Empty;
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
        IBitLockerService bitLocker,
        IGoogleDriveAuthService googleAuth,
        IUpdateService updates,
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
        _bitLocker = bitLocker;
        _googleAuth = googleAuth;
        _updates = updates;
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
    private string _workHoursStart = "06:00";

    [ObservableProperty]
    private string _workHoursEnd = "20:45";

    [ObservableProperty]
    private bool _allowOutOfHoursAppointments;

    [ObservableProperty]
    private bool _autoBackupEnabled = true;

    [ObservableProperty]
    private bool _backupEncryptionEnabled;

    [ObservableProperty]
    private string _backupPassword = string.Empty;

    [ObservableProperty]
    private string _backupPasswordConfirm = string.Empty;

    [ObservableProperty]
    private bool _googleDriveEnabled;

    [ObservableProperty]
    private string _googleClientId = string.Empty;

    [ObservableProperty]
    private string _googleClientSecret = string.Empty;

    [ObservableProperty]
    private string _googleFolderName = "CGP Yedekler";

    [ObservableProperty]
    private string _googleDriveFolder = string.Empty;

    [ObservableProperty]
    private string _googleDriveStatus = "Klasör seçilmedi.";

    [ObservableProperty]
    private string _updateStatusText = $"Sürüm {AppConstants.Version}";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _latestVersionText = string.Empty;

    [ObservableProperty]
    private bool _isConnectingDrive;

    [ObservableProperty]
    private string _bitLockerText = "Kontrol edilmedi.";

    [ObservableProperty]
    private bool _isCheckingBitLocker;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    public bool IsPortable => AppPaths.IsPortable;

    public string DataRootText => AppPaths.Root;

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
            WorkHoursStart = settings.WorkHoursStart;
            WorkHoursEnd = settings.WorkHoursEnd;
            AllowOutOfHoursAppointments = settings.AllowOutOfHoursAppointments;
            AutoBackupEnabled = settings.AutoBackupEnabled;
            BackupEncryptionEnabled = settings.BackupEncryptionEnabled;
            BackupPassword = settings.BackupPassword;
            BackupPasswordConfirm = settings.BackupPassword;
            GoogleDriveEnabled = settings.GoogleDriveEnabled;
            GoogleClientId = settings.GoogleClientId;
            GoogleClientSecret = settings.GoogleClientSecret;
            GoogleFolderName = settings.GoogleFolderName;
            GoogleDriveFolder = string.IsNullOrWhiteSpace(settings.GoogleDriveFolder)
                ? DetectDriveFolder() ?? string.Empty
                : settings.GoogleDriveFolder;
            GoogleDriveStatus = string.IsNullOrWhiteSpace(GoogleDriveFolder)
                ? "Klasör seçilmedi."
                : $"Klasör: {GoogleDriveFolder}";
            _googleRefreshToken = settings.GoogleRefreshToken;
            _googleAccountEmail = settings.GoogleAccountEmail;
            GoogleDriveStatus = string.IsNullOrWhiteSpace(settings.GoogleAccountEmail)
                ? "Bağlı değil."
                : $"Bağlı: {settings.GoogleAccountEmail}";
            SelectedAutoStartMode = AutoStartModeOptions.FirstOrDefault(x => x.Value == settings.AutoStartMode)
                                    ?? AutoStartModeOptions[0];
            SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Value == settings.Theme) ?? ThemeOptions[0];
            SelectedReminder = ReminderOptions.FirstOrDefault(x => x.Minutes == settings.DefaultReminderMinutes)
                               ?? ReminderOptions[0];

            _ = CheckBitLockerAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task CheckBitLockerAsync()
    {
        if (IsCheckingBitLocker)
            return;

        IsCheckingBitLocker = true;
        try
        {
            var status = await _bitLocker.GetStatusAsync();
            BitLockerText = status switch
            {
                Domain.Rules.BitLockerProtectionStatus.On =>
                    "Açık — disk şifrelemesi etkin.",
                Domain.Rules.BitLockerProtectionStatus.Off =>
                    "Kapalı — Windows'ta BitLocker'ı açmanız önerilir.",
                _ => "Kontrol edilemedi (yönetici hakkı veya BitLocker cmdlet'i gerekli)."
            };
        }
        finally
        {
            IsCheckingBitLocker = false;
        }
    }

    private AppSettings BuildSettings()
        => new(
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
            SmtpFromName,
            WorkHoursStart,
            WorkHoursEnd,
            AllowOutOfHoursAppointments,
            AutoBackupEnabled,
            BackupEncryptionEnabled,
            BackupPassword,
            GoogleDriveEnabled,
            GoogleClientId,
            GoogleClientSecret,
            _googleRefreshToken,
            _googleAccountEmail,
            GoogleFolderName,
            GoogleDriveFolder);

    [RelayCommand]
    private async Task ConnectGoogleDriveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (!BackupEncryptionEnabled)
        {
            ErrorMessage = "Google Drive'a yükleme için önce yedek şifrelemeyi açın.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GoogleClientId) || string.IsNullOrWhiteSpace(GoogleClientSecret))
        {
            ErrorMessage = "Google istemci kimliği ve gizli anahtarını girin.";
            return;
        }

        IsConnectingDrive = true;
        try
        {
            var result = await _googleAuth.ConnectAsync(GoogleClientId.Trim(), GoogleClientSecret.Trim());
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            _googleRefreshToken = result.RefreshToken ?? string.Empty;
            _googleAccountEmail = result.Email ?? string.Empty;
            GoogleDriveEnabled = true;
            GoogleDriveStatus = string.IsNullOrWhiteSpace(_googleAccountEmail)
                ? "Bağlı"
                : $"Bağlı: {_googleAccountEmail}";

            var saved = await _settings.SaveAsync(BuildSettings());
            if (!saved.Success)
            {
                ErrorMessage = saved.Error;
                return;
            }

            StatusMessage = "Google Drive bağlantısı kuruldu.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Google Drive bağlantısı kurulamadı: {ex.Message}";
        }
        finally
        {
            IsConnectingDrive = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectGoogleDriveAsync()
    {
        _googleRefreshToken = string.Empty;
        _googleAccountEmail = string.Empty;
        GoogleDriveEnabled = false;
        GoogleDriveStatus = "Bağlı değil.";

        var saved = await _settings.SaveAsync(BuildSettings());
        StatusMessage = saved.Success ? "Google Drive bağlantısı kesildi." : null;
        if (!saved.Success)
            ErrorMessage = saved.Error;
    }

    [RelayCommand]
    private void BrowseGoogleDriveFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Google Drive yedek klasörünü seçin"
        };

        if (Directory.Exists(GoogleDriveFolder))
            dialog.InitialDirectory = GoogleDriveFolder;

        if (dialog.ShowDialog() == true)
            GoogleDriveFolder = dialog.FolderName;
    }

    [RelayCommand]
    private void DetectGoogleDriveFolder()
    {
        var detected = DetectDriveFolder();
        if (detected is null)
        {
            ErrorMessage = "Google Drive klasörü bulunamadı. Drive for Desktop kurulu ve senkronize mi?";
            return;
        }

        GoogleDriveFolder = detected;
        GoogleDriveStatus = $"Klasör: {detected}";
        StatusMessage = "Google Drive klasörü bulundu.";
    }

    private static string? DetectDriveFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, "Google Drive", "My Drive", "CGP Yedekler"),
            Path.Combine(home, "Google Drive", "CGP Yedekler"),
            Path.Combine(home, "Google Drive", "My Drive"),
            Path.Combine(home, "Google Drive"),
            Path.Combine(home, "GoogleDrive")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (IsCheckingUpdate)
            return;

        IsCheckingUpdate = true;
        UpdateStatusText = "Güncellemeler kontrol ediliyor...";
        try
        {
            var result = await _updates.CheckAsync();
            if (result.Error is not null)
            {
                UpdateStatusText = result.Error;
                IsUpdateAvailable = false;
                return;
            }

            _lastUpdate = result;
            IsUpdateAvailable = result.UpdateAvailable;
            LatestVersionText = result.LatestVersion ?? string.Empty;
            UpdateStatusText = result.UpdateAvailable
                ? $"Yeni sürüm hazır: {result.LatestVersion} (mevcut {result.CurrentVersion})"
                : $"Güncel sürüm: {result.CurrentVersion}";
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsUpdateAvailable))]
    private async Task InstallUpdateAsync()
    {
        if (_lastUpdate is null || !_lastUpdate.UpdateAvailable)
            return;

        if (!_dialogs.Confirm(
                $"{_lastUpdate.LatestVersion} sürümü indirilip kurulacak. Uygulama yeniden başlayacak. Devam edilsin mi?",
                "Güncelleme"))
            return;

        IsCheckingUpdate = true;
        try
        {
            var result = await _updates.InstallAsync(_lastUpdate);
            if (!result.Success)
            {
                _dialogs.Error(result.Error ?? "Güncelleme başlatılamadı.", "Güncelleme");
                return;
            }

            _dialogs.Info("Güncelleme başlatıldı. Uygulama yeniden başlayacak.", "Güncelleme");
            System.Windows.Application.Current?.Shutdown();
        }
        finally
        {
            IsCheckingUpdate = false;
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

        if (BackupEncryptionEnabled
            && !string.Equals(BackupPassword, BackupPasswordConfirm, StringComparison.Ordinal))
        {
            ErrorMessage = "Yedek parolası ve doğrulaması aynı olmalıdır.";
            return;
        }

        IsBusy = true;

        try
        {
            var settings = BuildSettings();

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

    partial void OnIsUpdateAvailableChanged(bool value)
        => InstallUpdateCommand.NotifyCanExecuteChanged();

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
