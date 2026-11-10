using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Desktop.Views;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;
    private readonly ISettingsService _settings;
    private bool _suppressNavigation;

    public ShellViewModel(
        IServiceProvider services,
        IAuthService auth,
        ICurrentUser currentUser,
        ISettingsService settings)
    {
        _services = services;
        _auth = auth;
        _currentUser = currentUser;
        _settings = settings;

        WeakReferenceMessenger.Default.Register<NavigateMessage>(
            this,
            static (recipient, message) => ((ShellViewModel)recipient).NavigateTo(message.Key));

        WeakReferenceMessenger.Default.Register<InstitutionChangedMessage>(
            this,
            static (recipient, message) => ((ShellViewModel)recipient).InstitutionName = message.InstitutionName);
    }

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _pageTitle = "Günlük Liste";

    [ObservableProperty]
    private string _pageSubtitle = "Randevular, arama ve günlük akış";

    [ObservableProperty]
    private bool _isAppointmentsSelected = true;

    [ObservableProperty]
    private bool _isHostsSelected;

    [ObservableProperty]
    private bool _isDashboardSelected;

    [ObservableProperty]
    private bool _isCategoriesSelected;

    [ObservableProperty]
    private bool _isUsersSelected;

    [ObservableProperty]
    private bool _isSettingsSelected;

    [ObservableProperty]
    private bool _isBackupsSelected;

    [ObservableProperty]
    private bool _isAuditSelected;

    [ObservableProperty]
    private string _institutionName = AppConstants.DefaultInstitutionName;

    public string UserDisplayName => _currentUser.DisplayName;

    public string UserRoleText => _currentUser.Role switch
    {
        UserRole.Admin => "Yönetici",
        UserRole.Secretary => "Sekreter",
        UserRole.Host => "Görüşmeci",
        _ => "Kullanıcı"
    };

    public string UserInitials
    {
        get
        {
            var parts = _currentUser.DisplayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return "?";

            return parts.Length == 1
                ? parts[0][..1].ToUpperInvariant()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }

    public bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    public bool ShowHostsNav => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool ShowUsersNav => IsAdmin;

    public string VersionText => $"Sürüm {AppConstants.Version}";

    public string ProductName => AppConstants.ProductName;

    public string TodayText => DateTime.Now.ToString("dd/MM/yyyy");

    public event EventHandler? LogoutRequested;

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _settings.GetAsync();
            InstitutionName = AppConstants.ResolveInstitutionName(settings.InstitutionName);
        }
        catch
        {
            InstitutionName = AppConstants.DefaultInstitutionName;
        }

        NavigateTo("appointments");
    }

    [RelayCommand]
    private void OpenTodayAppointments()
    {
        NavigateTo("appointments");
        WeakReferenceMessenger.Default.Send(new OpenDateMessage(DateTime.Today));
    }

    [RelayCommand]
    private void OpenUserArea()
        => NavigateTo(ShowUsersNav ? "users" : "settings");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NavigateTo(string key)
    {
        if (key == "hosts" && !ShowHostsNav)
            key = "appointments";

        if (key == "users" && !ShowUsersNav)
            key = "appointments";

        if (key == "audit" && !IsAdmin)
            key = "appointments";

        _suppressNavigation = true;
        IsAppointmentsSelected = key == "appointments";
        IsHostsSelected = key == "hosts";
        IsDashboardSelected = key == "dashboard";
        IsCategoriesSelected = key == "categories";
        IsUsersSelected = key == "users";
        IsSettingsSelected = key == "settings";
        IsBackupsSelected = key == "backups";
        IsAuditSelected = key == "audit";
        _suppressNavigation = false;

        CurrentPage = key switch
        {
            "hosts" => _services.GetRequiredService<HostsPage>(),
            "categories" => _services.GetRequiredService<CategoriesPage>(),
            "users" => _services.GetRequiredService<UsersPage>(),
            "settings" => _services.GetRequiredService<SettingsPage>(),
            "backups" => _services.GetRequiredService<BackupsPage>(),
            "audit" => _services.GetRequiredService<AuditLogPage>(),
            "dashboard" => _services.GetRequiredService<DashboardPage>(),
            _ => _services.GetRequiredService<AppointmentListPage>()
        };

        (PageTitle, PageSubtitle) = key switch
        {
            "hosts" => ("Görüşmeciler", "Görüşme yapılacak kişiler ve giriş bilgileri"),
            "categories" => ("Kategoriler", "Görüşme konularını ekleyin, düzenleyin ve sıralayın"),
            "users" => ("Kullanıcılar", "Roller, giriş izinleri ve şifre yönetimi"),
            "settings" => ("Ayarlar", "Tema, bildirim, yedekleme ve tanılama"),
            "backups" => ("Yedekleme", "Yedek alın, doğrulayın ve geri yükleyin"),
            "audit" => ("Denetim Kaydı", "Kim, ne zaman, hangi işlemi yaptı?"),
            "dashboard" => ("Kontrol Paneli", "Sistem durumu ve yapılandırma özeti"),
            _ => ("Günlük Liste", "Randevular, arama ve günlük akış")
        };
    }

    partial void OnIsAppointmentsSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("appointments");
    }

    partial void OnIsHostsSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("hosts");
    }

    partial void OnIsDashboardSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("dashboard");
    }

    partial void OnIsCategoriesSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("categories");
    }

    partial void OnIsUsersSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("users");
    }

    partial void OnIsSettingsSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("settings");
    }

    partial void OnIsBackupsSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("backups");
    }

    partial void OnIsAuditSelectedChanged(bool value)
    {
        if (value && !_suppressNavigation)
            NavigateTo("audit");
    }

    public void Dispose()
        => WeakReferenceMessenger.Default.UnregisterAll(this);
}
