using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Notifications;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed partial class SetupStepItem : ObservableObject
{
    public SetupStepItem(int index, string title, bool isOptional)
    {
        Index = index;
        Title = title;
        IsOptional = isOptional;
    }

    public int Index { get; }
    public string Number => (Index + 1).ToString();
    public string Title { get; }
    public bool IsOptional { get; }

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isSkipped;

    public string Badge => IsSkipped ? "–" : IsCompleted ? "✓" : Number;

    public string OptionalLabel => IsSkipped ? "Atlandı" : "Atla";

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(OptionalLabel));
    }

    partial void OnIsSkippedChanged(bool value)
    {
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(OptionalLabel));
    }
}

/// <summary>
/// İlk kurulum ve ayar güncelleme sihirbazı. Adımlar sırayla doğrulanır;
/// zorunlu alanlar tamamlanmadan ilerlenemez. Opsiyonel adımlar atlanabilir.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject
{
    public const int WelcomeStep = 0;
    public const int InstitutionStep = 1;
    public const int AdminStep = 2;
    public const int PreferencesStep = 3;
    public const int SmtpStep = 4;
    public const int PushStep = 5;
    public const int BackupStep = 6;
    public const int SummaryStep = 7;

    private readonly IAuthService _auth;
    private readonly ISettingsService _settings;
    private readonly IUserRepository _users;
    private readonly ThemeService _theme;
    private readonly AutoStartService _autoStart;
    private readonly SmtpEmailSender _email;
    private readonly IPushNotifier _push;
    private readonly InstitutionLogoService _logo;
    private readonly IRememberedLoginStore _remembered;
    private readonly ICurrentUser _currentUser;

    private AppSettings? _loadedSettings;
    private readonly HashSet<int> _skippedSteps = new();
    private string _lastInstitutionEmail = string.Empty;
    private AppTheme _originalTheme = AppTheme.Auto;
    private string? _pendingLogoSource;
    private bool _logoRemoved;
    private bool _initialized;
    private bool _completed;
    private bool _isLoading;

    public SetupViewModel(
        IAuthService auth,
        ISettingsService settings,
        IUserRepository users,
        ThemeService theme,
        AutoStartService autoStart,
        SmtpEmailSender email,
        IPushNotifier push,
        InstitutionLogoService logo,
        IRememberedLoginStore remembered,
        ICurrentUser currentUser)
    {
        _auth = auth;
        _settings = settings;
        _users = users;
        _theme = theme;
        _autoStart = autoStart;
        _email = email;
        _push = push;
        _logo = logo;
        _remembered = remembered;
        _currentUser = currentUser;

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
            new("tray", "Gizli başlat (tepside)"),
            new("login", "Giriş ekranı açık")
        };

        Steps = new ObservableCollection<SetupStepItem>
        {
            new(WelcomeStep, "Hoş geldiniz", false),
            new(InstitutionStep, "Kurum bilgileri", false),
            new(AdminStep, "Yönetici hesabı", false),
            new(PreferencesStep, "Görünüm ve tercihler", false),
            new(SmtpStep, "E-posta (SMTP)", true),
            new(PushStep, "Telefon bildirimleri", true),
            new(BackupStep, "Yedekleme", true),
            new(SummaryStep, "Özet ve tamamla", false)
        };

        RefreshStepStates();
    }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<ReminderOption> ReminderOptions { get; }
    public ObservableCollection<string> PushProviderOptions { get; }
    public ObservableCollection<SetupStepItem> Steps { get; }

    public string ProductName => AppConstants.ProductName;
    public string VersionText => $"Sürüm {AppConstants.Version}";
    public string LogoFormatsText => InstitutionLogoService.SupportedFormatsText;

    public event EventHandler? SetupSucceeded;

    // ---- Adım durumu ----

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private bool _isUpdateMode;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsFirstRunMode => !IsUpdateMode;

    public string WindowTitle => IsUpdateMode
        ? "Kurulum ve ayarlar · CGP - Randevu Takip"
        : "İlk kurulum · CGP - Randevu Takip";

    public bool IsWelcomeStep => CurrentStep == WelcomeStep;
    public bool IsInstitutionStep => CurrentStep == InstitutionStep;
    public bool IsAdminStep => CurrentStep == AdminStep;
    public bool IsPreferencesStep => CurrentStep == PreferencesStep;
    public bool IsSmtpStep => CurrentStep == SmtpStep;
    public bool IsPushStep => CurrentStep == PushStep;
    public bool IsBackupStep => CurrentStep == BackupStep;
    public bool IsSummaryStep => CurrentStep == SummaryStep;

    public bool IsFirstStep => CurrentStep == WelcomeStep;
    public bool CanSkipCurrentStep => CurrentStep is SmtpStep or PushStep or BackupStep;

    public string ContinueText => IsSummaryStep ? "Kurulumu tamamla" : "İleri";

    public string HeaderTitle => CurrentStep switch
    {
        WelcomeStep => "Kuruluma hoş geldiniz",
        InstitutionStep => "Kurum bilgileri",
        AdminStep => IsUpdateMode ? "Yönetici hesabı" : "Yönetici hesabı oluşturun",
        PreferencesStep => "Görünüm ve çalışma tercihleri",
        SmtpStep => "E-posta / SMTP (isteğe bağlı)",
        PushStep => "Telefon bildirimleri (isteğe bağlı)",
        BackupStep => "Yedekleme (isteğe bağlı)",
        _ => "Özet ve tamamla"
    };

    public string HeaderSubtitle => CurrentStep switch
    {
        WelcomeStep => "Birkaç adımda kurumunuzu, yönetici hesabınızı ve isteğe bağlı entegrasyonları yapılandırın.",
        InstitutionStep => "Bu bilgiler başlıkta, günlük paylaşım görselinde ve e-posta gönderiminde kullanılır.",
        AdminStep => IsUpdateMode
            ? "E-posta adresinizi güncelleyebilir, isterseniz şifrenizi değiştirebilirsiniz."
            : "Bu hesap tüm ayarlara, kullanıcı yönetimine ve kategorilere erişebilir.",
        PreferencesStep => "Tema seçildiği anda uygulanır. Diğer değerler kurulum tamamlanınca kaydedilir.",
        SmtpStep => "Şifre sıfırlama e-postaları ve test gönderimleri için kullanılır. Bu adımı atlayabilirsiniz.",
        PushStep => "Windows bildirimlerine ek olarak telefonunuza bildirim göndermek için kullanılır.",
        BackupStep => "Otomatik yedekler ve isteğe bağlı Google Drive for Desktop kopyası.",
        _ => "Bilgileri kontrol edin. Kurulumu tamamladığınızda ayarlar kaydedilir."
    };

    // ---- Kurum ----

    [ObservableProperty]
    private string _institutionName = string.Empty;

    [ObservableProperty]
    private string _institutionEmail = string.Empty;

    [ObservableProperty]
    private string _institutionPhone = string.Empty;

    [ObservableProperty]
    private string _institutionAddress = string.Empty;

    [ObservableProperty]
    private BitmapImage? _logoPreview;

    [ObservableProperty]
    private string _logoRelativePath = string.Empty;

    public bool HasLogo => LogoPreview is not null;

    // ---- Yönetici ----

    [ObservableProperty]
    private string _adminDisplayName = string.Empty;

    [ObservableProperty]
    private string _adminUsername = string.Empty;

    [ObservableProperty]
    private string _adminEmail = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _changePassword;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _newConfirmPassword = string.Empty;

    // ---- Tercihler ----

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    [ObservableProperty]
    private ReminderOption? _selectedReminder;

    [ObservableProperty]
    private string _workHoursStart = "06:00";

    [ObservableProperty]
    private string _workHoursEnd = "20:45";

    [ObservableProperty]
    private string _dailyShareTime = "08:30";

    [ObservableProperty]
    private bool _autoStartEnabled = true;

    public ObservableCollection<AutoStartModeOption> AutoStartModeOptions { get; }

    [ObservableProperty]
    private AutoStartModeOption? _selectedAutoStartMode;

    [ObservableProperty]
    private bool _closeToTray = true;

    // ---- SMTP ----

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
    private string _smtpFromName = string.Empty;

    [ObservableProperty]
    private string _testEmailAddress = string.Empty;

    [ObservableProperty]
    private string? _smtpTestMessage;

    // ---- Push ----

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
    private string? _pushTestMessage;

    public bool IsNtfyProvider => !string.Equals(SelectedPushProvider, "telegram", StringComparison.OrdinalIgnoreCase);

    public bool IsTelegramProvider => string.Equals(SelectedPushProvider, "telegram", StringComparison.OrdinalIgnoreCase);

    // ---- Yedekleme ----

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

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
    private string _googleDriveFolder = string.Empty;

    [ObservableProperty]
    private string _googleDriveStatus = "Bağlı değil.";

    // ---- Özet metinleri ----

    public string SummaryInstitutionText =>
        $"{InstitutionName} · {InstitutionEmail} · {InstitutionPhone}";

    public string SummaryAddressText => InstitutionAddress;

    public string SummaryAdminText => $"{AdminUsername} · {AdminEmail}";

    public string SummaryLogoText => HasLogo ? "Logo seçildi" : "Logo eklenmedi";

    public string SummaryPreferencesText =>
        $"Tema: {SelectedTheme?.Label ?? "Otomatik"} · Hatırlatıcı: {SelectedReminder?.Label ?? "30 dakika önce"} · " +
        $"Çalışma: {WorkHoursStart}–{WorkHoursEnd} · Paylaşım: {DailyShareTime}";

    public string SummarySmtpText => _skippedSteps.Contains(SmtpStep)
        ? (IsUpdateMode
            ? "Atlandı — mevcut e-posta ayarları korundu."
            : "Atlandı — daha sonra Ayarlar > E-posta bölümünden yapılandırılabilir.")
        : string.IsNullOrWhiteSpace(SmtpHost)
            ? "Yapılandırılmadı — Ayarlar > E-posta bölümünden tamamlanabilir."
            : $"{SmtpHost}:{SmtpPort} · {SmtpFromAddress}";

    public string SummaryPushText => _skippedSteps.Contains(PushStep)
        ? (IsUpdateMode
            ? "Atlandı — mevcut bildirim ayarları korundu."
            : "Atlandı — daha sonra Ayarlar > Bildirimler bölümünden açılabilir.")
        : !PushEnabled
            ? "Kapalı — Ayarlar > Bildirimler bölümünden açılabilir."
            : $"{SelectedPushProvider} · " +
              (SelectedPushProvider == "telegram" ? $"chat {PushChatId}" : $"topic {PushTopic}");

    public string SummaryBackupText => _skippedSteps.Contains(BackupStep)
        ? (IsUpdateMode
            ? $"Atlandı — mevcut yedekleme ayarları korundu ({BackupDirectory})."
            : $"Atlandı — varsayılan yedek klasörü kullanılacak ({BackupDirectory}).")
        : $"{BackupDirectory} · Otomatik yedek {(AutoBackupEnabled ? "açık" : "kapalı")} · " +
          $"Şifreleme {(BackupEncryptionEnabled ? "açık" : "kapalı")}" +
          (GoogleDriveEnabled ? $" · Drive: {GoogleDriveFolder}" : string.Empty);

    public string SummaryAdminSecurityText => IsUpdateMode
        ? (ChangePassword ? "Şifre bu kurulumda güncellenecek." : "Şifre değiştirilmeyecek.")
        : "Yönetici hesabı kurulum tamamlanınca oluşturulacak.";

    // ---- Başlatma ----

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        _isLoading = true;
        IsBusy = true;

        try
        {
            IsUpdateMode = !await _auth.IsFirstRunAsync();
            OnPropertyChanged(nameof(IsFirstRunMode));
            OnPropertyChanged(nameof(WindowTitle));

            var settings = await _settings.GetAsync();
            _loadedSettings = settings;
            _lastInstitutionEmail = settings.InstitutionEmail;

            InstitutionName = settings.InstitutionName;
            InstitutionEmail = settings.InstitutionEmail;
            InstitutionPhone = settings.InstitutionPhone;
            InstitutionAddress = settings.InstitutionAddress;
            LogoRelativePath = settings.InstitutionLogoFile;
            LogoPreview = _logo.LoadPreview(settings.InstitutionLogoFile);

            WorkHoursStart = settings.WorkHoursStart;
            WorkHoursEnd = settings.WorkHoursEnd;
            DailyShareTime = settings.DailyShareTime;
            AutoStartEnabled = settings.AutoStartEnabled;
            SelectedAutoStartMode = AutoStartModeOptions.FirstOrDefault(x => x.Value == settings.AutoStartMode)
                                    ?? AutoStartModeOptions[0];
            CloseToTray = settings.CloseToTray;

            SmtpHost = settings.SmtpHost;
            SmtpPort = settings.SmtpPort.ToString();
            SmtpUseSsl = settings.SmtpUseSsl;
            SmtpUsername = settings.SmtpUsername;
            SmtpPassword = settings.SmtpPassword;
            SmtpFromAddress = string.IsNullOrWhiteSpace(settings.SmtpFromAddress)
                ? settings.InstitutionEmail
                : settings.SmtpFromAddress;
            SmtpFromName = settings.SmtpFromName;
            TestEmailAddress = settings.InstitutionEmail;

            PushEnabled = settings.PushEnabled;
            SelectedPushProvider = settings.PushProvider;
            PushTopic = settings.PushTopic;
            PushToken = settings.PushToken;
            PushChatId = settings.PushChatId;
            QuietHoursEnabled = settings.QuietHoursEnabled;
            QuietHoursStart = settings.QuietHoursStart;
            QuietHoursEnd = settings.QuietHoursEnd;

            BackupDirectory = settings.BackupDirectory;
            AutoBackupEnabled = settings.AutoBackupEnabled;
            BackupEncryptionEnabled = settings.BackupEncryptionEnabled;
            BackupPassword = settings.BackupPassword;
            BackupPasswordConfirm = settings.BackupPassword;
            GoogleDriveEnabled = settings.GoogleDriveEnabled;
            GoogleDriveFolder = settings.GoogleDriveFolder;
            GoogleDriveStatus = string.IsNullOrWhiteSpace(settings.GoogleDriveFolder)
                ? "Bağlı değil."
                : $"Klasör: {settings.GoogleDriveFolder}";

            SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Value == settings.Theme)
                            ?? ThemeOptions[0];
            SelectedReminder = ReminderOptions.FirstOrDefault(x => x.Minutes == settings.DefaultReminderMinutes)
                               ?? ReminderOptions[0];

            if (IsUpdateMode)
            {
                AdminDisplayName = _currentUser.DisplayName;
                AdminUsername = _currentUser.Username;

                if (_currentUser.UserId is { } userId)
                {
                    var user = await _users.GetByIdAsync(userId);
                    if (user is not null)
                    {
                        AdminDisplayName = user.DisplayName;
                        AdminUsername = user.Username;
                        AdminEmail = user.Email ?? settings.InstitutionEmail;
                    }
                }

                if (string.IsNullOrWhiteSpace(AdminEmail))
                    AdminEmail = settings.InstitutionEmail;
            }

            _originalTheme = settings.Theme;
            _theme.Apply(settings.Theme);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kurulum bilgileri yüklenemedi: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
            RefreshStepStates();
        }
    }

    public void RestoreThemeOnCancel()
    {
        if (!_completed)
            _theme.Apply(_originalTheme);
    }

    // ---- Gezinme ----

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        if (IsBusy)
            return;

        if (IsSummaryStep)
        {
            await FinishAsync();
            return;
        }

        ErrorMessage = ValidateStep(CurrentStep);
        if (ErrorMessage is not null)
            return;

        _skippedSteps.Remove(CurrentStep);
        NotifySummary();
        MoveTo(CurrentStep + 1);
    }

    private bool CanContinue() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (CanGoBack())
            MoveTo(CurrentStep - 1);
    }

    private bool CanGoBack() => !IsBusy && CurrentStep > WelcomeStep;

    [RelayCommand(CanExecute = nameof(CanSkipCurrentStep))]
    private void Skip()
    {
        if (!CanSkipCurrentStep || IsBusy)
            return;

        RestoreStepDefaults(CurrentStep);
        _skippedSteps.Add(CurrentStep);
        ErrorMessage = null;
        NotifySummary();
        MoveTo(CurrentStep + 1);
    }

    /// <summary>
    /// "Atla" seçilen adımda kullanıcı bir şey girmiş olsa bile mevcut/kayıtlı
    /// değerler korunur; yarım yapılandırma kaydedilmez.
    /// </summary>
    private void RestoreStepDefaults(int step)
    {
        var settings = _loadedSettings;
        if (settings is null)
            return;

        switch (step)
        {
            case SmtpStep:
                SmtpHost = settings.SmtpHost;
                SmtpPort = settings.SmtpPort.ToString();
                SmtpUseSsl = settings.SmtpUseSsl;
                SmtpUsername = settings.SmtpUsername;
                SmtpPassword = settings.SmtpPassword;
                SmtpFromAddress = string.IsNullOrWhiteSpace(settings.SmtpFromAddress)
                    ? settings.InstitutionEmail
                    : settings.SmtpFromAddress;
                SmtpFromName = settings.SmtpFromName;
                SmtpTestMessage = null;
                break;
            case PushStep:
                PushEnabled = settings.PushEnabled;
                SelectedPushProvider = settings.PushProvider;
                PushTopic = settings.PushTopic;
                PushToken = settings.PushToken;
                PushChatId = settings.PushChatId;
                QuietHoursEnabled = settings.QuietHoursEnabled;
                QuietHoursStart = settings.QuietHoursStart;
                QuietHoursEnd = settings.QuietHoursEnd;
                PushTestMessage = null;
                break;
            case BackupStep:
                BackupDirectory = settings.BackupDirectory;
                AutoBackupEnabled = settings.AutoBackupEnabled;
                BackupEncryptionEnabled = settings.BackupEncryptionEnabled;
                BackupPassword = settings.BackupPassword;
                BackupPasswordConfirm = settings.BackupPassword;
                GoogleDriveEnabled = settings.GoogleDriveEnabled;
                GoogleDriveFolder = settings.GoogleDriveFolder;
                GoogleDriveStatus = string.IsNullOrWhiteSpace(settings.GoogleDriveFolder)
                    ? "Bağlı değil."
                    : $"Klasör: {settings.GoogleDriveFolder}";
                break;
        }
    }

    private void MoveTo(int step)
    {
        ErrorMessage = null;
        CurrentStep = Math.Clamp(step, WelcomeStep, SummaryStep);
    }

    private string? ValidateStep(int step) => step switch
    {
        InstitutionStep => SetupValidator.ValidateInstitution(
            InstitutionName,
            InstitutionEmail,
            InstitutionPhone,
            InstitutionAddress),
        AdminStep => ValidateAdminStep(),
        PreferencesStep => SetupValidator.ValidatePreferences(
            SelectedReminder?.Minutes ?? 30,
            WorkHoursStart,
            WorkHoursEnd,
            DailyShareTime)
            ?? (QuietHoursEnabled
                ? SetupValidator.TryParseTime(QuietHoursStart, out _)
                  && SetupValidator.TryParseTime(QuietHoursEnd, out _)
                    ? null
                    : "Sessiz saatler SS:dd biçiminde olmalıdır."
                : null),
        SmtpStep => SetupValidator.ValidateSmtp(
            SmtpHost,
            SmtpPort,
            SmtpFromAddress,
            SmtpUsername,
            SmtpPassword),
        PushStep => SetupValidator.ValidatePush(
            PushEnabled,
            SelectedPushProvider,
            PushTopic,
            PushToken,
            PushChatId),
        BackupStep => ValidateBackupStep(),
        _ => null
    };

    private string? ValidateAdminStep()
    {
        if (!IsUpdateMode)
        {
            return SetupValidator.ValidateAdmin(
                AdminDisplayName,
                AdminUsername,
                AdminEmail,
                Password,
                ConfirmPassword);
        }

        var emailError = InstitutionProfilePolicy.ValidateEmail(AdminEmail, required: true);
        if (emailError is not null)
            return emailError;

        return ChangePassword
            ? SetupValidator.ValidateNewPassword(CurrentPassword, NewPassword, NewConfirmPassword)
            : null;
    }

    private string? ValidateBackupStep()
    {
        var error = SetupValidator.ValidateBackup(
            BackupDirectory,
            BackupEncryptionEnabled,
            BackupPassword,
            BackupPasswordConfirm,
            GoogleDriveEnabled,
            GoogleDriveFolder);
        if (error is not null)
            return error;

        if (GoogleDriveEnabled && !Directory.Exists(GoogleDriveFolder))
            return "Google Drive klasörü bulunamadı. Drive for Desktop senkronizasyonunu kontrol edin.";

        return null;
    }

    // ---- Tamamlama ----

    private async Task FinishAsync()
    {
        ErrorMessage = null;

        for (var step = InstitutionStep; step < SummaryStep; step++)
        {
            var error = ValidateStep(step);
            if (error is null)
                continue;

            MoveTo(step);
            ErrorMessage = error;
            return;
        }

        IsBusy = true;
        try
        {
            var logoRelative = LogoRelativePath;
            var hadLogoBefore = !string.IsNullOrWhiteSpace(LogoRelativePath);
            var logoImportedThisRun = false;

            if (_pendingLogoSource is not null)
            {
                var imported = _logo.Import(_pendingLogoSource);
                if (!imported.Success)
                {
                    MoveTo(InstitutionStep);
                    ErrorMessage = imported.Error;
                    return;
                }

                logoRelative = imported.RelativePath ?? string.Empty;
                _pendingLogoSource = null;
                logoImportedThisRun = true;
            }
            else if (_logoRemoved)
            {
                _logo.Remove();
                logoRelative = string.Empty;
                _logoRemoved = false;
            }

            if (!IsUpdateMode && await _auth.IsFirstRunAsync())
            {
                var setup = await _auth.CreateFirstAdminAsync(new SetupRequest(
                    AdminUsername,
                    AdminDisplayName,
                    AdminEmail,
                    Password,
                    ConfirmPassword));

                if (!setup.Success)
                {
                    MoveTo(AdminStep);
                    ErrorMessage = setup.Error;
                    return;
                }

                // İlk kurulumda da süresiz oturum token'ı üretilir.
                var firstToken = await _auth.CreateRememberTokenAsync();
                if (!string.IsNullOrWhiteSpace(firstToken))
                    _remembered.Save(firstToken);
            }
            else
            {
                var emailResult = await _auth.UpdateAdminEmailAsync(AdminEmail);
                if (!emailResult.Success)
                {
                    MoveTo(AdminStep);
                    ErrorMessage = emailResult.Error;
                    return;
                }

                if (ChangePassword)
                {
                    var passwordResult = await _auth.ChangePasswordAsync(
                        CurrentPassword,
                        NewPassword,
                        NewConfirmPassword);

                    if (!passwordResult.Success)
                    {
                        MoveTo(AdminStep);
                        ErrorMessage = passwordResult.Error;
                        return;
                    }

                    // Şifre değişimi kalıcı token'ı iptal eder; oturumun sürmesi için
                    // aynı kullanıcı adına yeni bir süresiz token üretilir.
                    var token = await _auth.CreateRememberTokenAsync();
                    if (!string.IsNullOrWhiteSpace(token))
                        _remembered.Save(token);

                    // "Beni hatırla" ile kayıtlı kimlik varsa eski şifre kalmasın.
                    if (_remembered.LoadCredentials() is not null)
                        _remembered.SaveCredentials(AdminUsername, NewPassword);
                }
            }

            var settings = BuildSettings(logoRelative);
            var saved = await _settings.SaveAsync(settings);
            if (!saved.Success)
            {
                // Kayıt başarısız olduysa ve önceden logo yoksa, bu çalıştırmada
                // kopyalanan dosyayı geri al; yarım kalan artık bırakma.
                if (logoImportedThisRun && !hadLogoBefore)
                {
                    _logo.Remove();
                    LogoPreview = null;
                    LogoRelativePath = string.Empty;
                }

                MoveTo(SummaryStep);
                ErrorMessage = saved.Error;
                return;
            }

            _theme.Apply(settings.Theme);
            _autoStart.SetEnabled(settings.AutoStartEnabled, settings.AutoStartMode);
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(settings.CloseToTray));
            WeakReferenceMessenger.Default.Send(
                new InstitutionChangedMessage(AppConstants.ResolveInstitutionName(settings.InstitutionName)));

            _completed = true;
            SetupSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MoveTo(SummaryStep);
            ErrorMessage = $"Kurulum tamamlanamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AppSettings BuildSettings(string logoRelativePath)
    {
        var baseline = _loadedSettings
                       ?? AppSettings.CreateDefault(
                           Path.Combine(
                               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                               "CgpRandevuTakip",
                               "Yedekler"));

        return baseline with
        {
            Theme = SelectedTheme?.Value ?? AppTheme.Auto,
            InstitutionName = InstitutionName.Trim(),
            InstitutionEmail = InstitutionEmail.Trim(),
            InstitutionPhone = InstitutionPhone.Trim(),
            InstitutionAddress = InstitutionAddress.Trim(),
            InstitutionLogoFile = logoRelativePath,
            BackupDirectory = BackupDirectory.Trim(),
            DefaultReminderMinutes = SelectedReminder?.Minutes ?? 30,
            PushEnabled = PushEnabled,
            PushProvider = SelectedPushProvider,
            PushTopic = PushTopic.Trim(),
            PushToken = PushToken.Trim(),
            PushChatId = PushChatId.Trim(),
            DailyShareTime = DailyShareTime.Trim(),
            QuietHoursEnabled = QuietHoursEnabled,
            QuietHoursStart = QuietHoursStart.Trim(),
            QuietHoursEnd = QuietHoursEnd.Trim(),
            AutoStartEnabled = AutoStartEnabled,
            AutoStartMode = AutoStartEnabled
                ? (SelectedAutoStartMode?.Value ?? "tray")
                : baseline.AutoStartMode,
            CloseToTray = CloseToTray,
            SmtpHost = SmtpHost.Trim(),
            SmtpPort = int.TryParse(SmtpPort, out var port) ? port : 587,
            SmtpUseSsl = SmtpUseSsl,
            SmtpUsername = SmtpUsername.Trim(),
            SmtpPassword = SmtpPassword,
            SmtpFromAddress = string.IsNullOrWhiteSpace(SmtpFromAddress)
                ? InstitutionEmail.Trim()
                : SmtpFromAddress.Trim(),
            SmtpFromName = SmtpFromName.Trim(),
            WorkHoursStart = WorkHoursStart.Trim(),
            WorkHoursEnd = WorkHoursEnd.Trim(),
            AutoBackupEnabled = AutoBackupEnabled,
            BackupEncryptionEnabled = BackupEncryptionEnabled,
            BackupPassword = BackupPassword,
            GoogleDriveEnabled = GoogleDriveEnabled,
            GoogleDriveFolder = GoogleDriveFolder.Trim()
        };
    }

    // ---- Komutlar: logo, klasör, testler ----

    [RelayCommand]
    private void PickLogo()
    {
        ErrorMessage = null;

        var dialog = new OpenFileDialog
        {
            Title = "Kurum logosunu seçin",
            Filter = "Görsel dosyaları|*.png;*.jpg;*.jpeg;*.bmp|Tüm dosyalar|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        var preview = _logo.LoadPreview(dialog.FileName);
        if (preview is null)
        {
            ErrorMessage = "Seçilen dosya bir görsel olarak okunamadı.";
            return;
        }

        _pendingLogoSource = dialog.FileName;
        _logoRemoved = false;
        LogoPreview = preview;
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        _pendingLogoSource = null;
        _logoRemoved = true;
        LogoPreview = null;
        LogoRelativePath = string.Empty;
    }

    [RelayCommand]
    private void BrowseBackupDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Yedek klasörünü seçin"
        };

        if (Directory.Exists(BackupDirectory))
            dialog.InitialDirectory = BackupDirectory;

        if (dialog.ShowDialog() == true)
            BackupDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseGoogleDriveFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Google Drive yedek klasörünü seçin"
        };

        if (Directory.Exists(GoogleDriveFolder))
            dialog.InitialDirectory = GoogleDriveFolder;

        if (dialog.ShowDialog() == true)
            SetDriveFolder(dialog.FolderName);
    }

    [RelayCommand]
    private void DetectGoogleDriveFolder()
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

        var detected = candidates.FirstOrDefault(Directory.Exists);
        if (detected is null)
        {
            ErrorMessage = "Google Drive klasörü bulunamadı. Drive for Desktop kurulu ve senkronize mi?";
            return;
        }

        SetDriveFolder(detected);
    }

    private void SetDriveFolder(string folder)
    {
        GoogleDriveFolder = folder;
        GoogleDriveStatus = $"Klasör: {folder}";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SendTestEmailAsync()
    {
        SmtpTestMessage = null;
        ErrorMessage = null;

        var error = SetupValidator.ValidateSmtp(
            SmtpHost,
            SmtpPort,
            SmtpFromAddress,
            SmtpUsername,
            SmtpPassword);
        if (error is not null)
        {
            SmtpTestMessage = error;
            return;
        }

        if (string.IsNullOrWhiteSpace(SmtpHost))
        {
            SmtpTestMessage = "Test için önce SMTP sunucusunu girin.";
            return;
        }

        var recipient = string.IsNullOrWhiteSpace(TestEmailAddress)
            ? SmtpFromAddress
            : TestEmailAddress.Trim();

        if (!EmailAddressPolicy.TryNormalize(recipient, out _))
        {
            SmtpTestMessage = "Test alıcısı geçerli bir e-posta adresi olmalıdır.";
            return;
        }

        IsBusy = true;
        try
        {
            var testSettings = BuildSettings(LogoRelativePath);
            var result = await _email.SendWithSettingsAsync(
                testSettings,
                recipient,
                "CGP - Randevu Takip · SMTP testi",
                $"Bu bir test e-postasıdır. Gönderim zamanı: {DateTime.Now:dd.MM.yyyy HH:mm}");

            SmtpTestMessage = result.Success
                ? $"Test e-postası gönderildi: {recipient}"
                : result.Error;
        }
        catch (Exception ex)
        {
            SmtpTestMessage = $"Test e-postası gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendTestPushAsync()
    {
        PushTestMessage = null;
        ErrorMessage = null;

        var error = SetupValidator.ValidatePush(
            PushEnabled,
            SelectedPushProvider,
            PushTopic,
            PushToken,
            PushChatId);
        if (error is not null)
        {
            PushTestMessage = error;
            return;
        }

        if (!PushEnabled)
        {
            PushTestMessage = "Test için önce telefon bildirimlerini açın.";
            return;
        }

        IsBusy = true;
        try
        {
            var sent = await _push.SendAsync(new PushRequest(
                SelectedPushProvider,
                PushTopic.Trim(),
                PushToken.Trim(),
                PushChatId.Trim(),
                "CGP Test",
                $"Test bildirimi · {DateTime.Now:HH:mm}"));

            PushTestMessage = sent
                ? "Test bildirimi gönderildi."
                : SelectedPushProvider == "telegram"
                    ? "Telegram testi başarısız. Bot token, sayısal chat id ve bota /start gönderildiğinden emin olun."
                    : "ntfy testi başarısız. Topic adını ve internet bağlantısını kontrol edin.";
        }
        catch (Exception ex)
        {
            PushTestMessage = $"Test bildirimi gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Değişiklik bildirimleri ----

    partial void OnCurrentStepChanged(int value)
    {
        RefreshStepStates();
        ClearError();

        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsInstitutionStep));
        OnPropertyChanged(nameof(IsAdminStep));
        OnPropertyChanged(nameof(IsPreferencesStep));
        OnPropertyChanged(nameof(IsSmtpStep));
        OnPropertyChanged(nameof(IsPushStep));
        OnPropertyChanged(nameof(IsBackupStep));
        OnPropertyChanged(nameof(IsSummaryStep));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(CanSkipCurrentStep));
        OnPropertyChanged(nameof(ContinueText));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));

        ContinueCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();

        if (value == SummaryStep)
            NotifySummary();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ContinueCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsUpdateModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsFirstRunMode));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    partial void OnLogoPreviewChanged(BitmapImage? value) => OnPropertyChanged(nameof(HasLogo));

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_isLoading || value is null)
            return;

        _theme.Apply(value.Value);
    }

    partial void OnChangePasswordChanged(bool value) => OnPropertyChanged(nameof(SummaryAdminSecurityText));

    partial void OnInstitutionNameChanged(string value) => NotifySummary();
    partial void OnInstitutionEmailChanged(string value)
    {
        if (!_isLoading)
        {
            var email = value?.Trim() ?? string.Empty;

            // Kurum e-postası SMTP gönderen adresinin varsayılanıdır.
            if (string.IsNullOrWhiteSpace(SmtpFromAddress)
                || string.Equals(SmtpFromAddress, _lastInstitutionEmail, StringComparison.OrdinalIgnoreCase))
            {
                SmtpFromAddress = email;
            }

            if (string.IsNullOrWhiteSpace(TestEmailAddress)
                || string.Equals(TestEmailAddress, _lastInstitutionEmail, StringComparison.OrdinalIgnoreCase))
            {
                TestEmailAddress = email;
            }

            _lastInstitutionEmail = email;
        }

        NotifySummary();
    }
    partial void OnInstitutionPhoneChanged(string value) => NotifySummary();
    partial void OnInstitutionAddressChanged(string value) => NotifySummary();
    partial void OnAdminUsernameChanged(string value) => NotifySummary();
    partial void OnAdminEmailChanged(string value) => NotifySummary();
    partial void OnSmtpHostChanged(string value) => NotifySummary();
    partial void OnSmtpPortChanged(string value) => NotifySummary();
    partial void OnSmtpFromAddressChanged(string value) => NotifySummary();
    partial void OnPushEnabledChanged(bool value) => NotifySummary();
    partial void OnSelectedPushProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsNtfyProvider));
        OnPropertyChanged(nameof(IsTelegramProvider));
        NotifySummary();
    }
    partial void OnPushTopicChanged(string value) => NotifySummary();
    partial void OnPushChatIdChanged(string value) => NotifySummary();
    partial void OnBackupDirectoryChanged(string value) => NotifySummary();
    partial void OnAutoBackupEnabledChanged(bool value) => NotifySummary();
    partial void OnBackupEncryptionEnabledChanged(bool value) => NotifySummary();
    partial void OnGoogleDriveEnabledChanged(bool value) => NotifySummary();
    partial void OnGoogleDriveFolderChanged(string value) => NotifySummary();
    partial void OnSelectedReminderChanged(ReminderOption? value) => NotifySummary();
    partial void OnWorkHoursStartChanged(string value) => NotifySummary();
    partial void OnWorkHoursEndChanged(string value) => NotifySummary();
    partial void OnDailyShareTimeChanged(string value) => NotifySummary();

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(SummaryInstitutionText));
        OnPropertyChanged(nameof(SummaryAddressText));
        OnPropertyChanged(nameof(SummaryAdminText));
        OnPropertyChanged(nameof(SummaryLogoText));
        OnPropertyChanged(nameof(SummaryPreferencesText));
        OnPropertyChanged(nameof(SummarySmtpText));
        OnPropertyChanged(nameof(SummaryPushText));
        OnPropertyChanged(nameof(SummaryBackupText));
        OnPropertyChanged(nameof(SummaryAdminSecurityText));
    }

    private void ClearError() => ErrorMessage = null;

    private void RefreshStepStates()
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsCurrent = i == CurrentStep;
            Steps[i].IsSkipped = _skippedSteps.Contains(i);
            Steps[i].IsCompleted = i < CurrentStep && !Steps[i].IsSkipped;
        }
    }
}
