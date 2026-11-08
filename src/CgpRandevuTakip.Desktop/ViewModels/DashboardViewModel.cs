using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ICategoryRepository _categories;
    private readonly IUserRepository _users;
    private readonly ISettingsService _settings;
    private readonly IAppEnvironment _environment;
    private readonly ICurrentUser _currentUser;

    public DashboardViewModel(
        ICategoryRepository categories,
        IUserRepository users,
        ISettingsService settings,
        IAppEnvironment environment,
        ICurrentUser currentUser)
    {
        _categories = categories;
        _users = users;
        _settings = settings;
        _environment = environment;
        _currentUser = currentUser;

        WeakReferenceMessenger.Default.Register<InstitutionChangedMessage>(
            this,
            static (recipient, message) => ((DashboardViewModel)recipient).InstitutionName = message.InstitutionName);
    }

    [ObservableProperty]
    private int _activeCategoryCount;

    [ObservableProperty]
    private int _totalCategoryCount;

    [ObservableProperty]
    private int _userCount;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _logsDirectory = string.Empty;

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private string _institutionName = AppConstants.DefaultInstitutionName;

    [ObservableProperty]
    private string _themeText = "Otomatik";

    [ObservableProperty]
    private string _reminderText = "30 dakika önce";

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _roleText = string.Empty;

    [ObservableProperty]
    private string _lastRefreshedText = "—";

    public string VersionText => $"Sürüm {AppConstants.Version}";

    public string SchemaVersionText => $"v{AppConstants.SchemaVersion}";

    public string Copyright => AppConstants.Copyright;

    public string CategorySummary => $"Toplam {TotalCategoryCount} kategori";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var categoriesTask = _categories.GetAllAsync();
        var settingsTask = _settings.GetAsync();
        var userCountTask = _users.CountAsync();

        await Task.WhenAll(categoriesTask, settingsTask, userCountTask);

        var all = await categoriesTask;
        var settings = await settingsTask;

        ActiveCategoryCount = all.Count(x => x.IsActive);
        TotalCategoryCount = all.Count;
        UserCount = await userCountTask;

        DatabasePath = _environment.DatabasePath;
        LogsDirectory = _environment.LogsDirectory;
        BackupDirectory = settings.BackupDirectory;

        InstitutionName = AppConstants.ResolveInstitutionName(settings.InstitutionName);

        ThemeText = settings.Theme switch
        {
            AppTheme.Light => "Açık",
            AppTheme.Dark => "Koyu",
            _ => "Otomatik"
        };

        ReminderText = settings.DefaultReminderMinutes switch
        {
            1440 => "1 gün önce",
            60 => "1 saat önce",
            _ => "30 dakika önce"
        };

        Greeting = $"Hoş geldiniz, {_currentUser.DisplayName}";
        RoleText = _currentUser.Role switch
        {
            UserRole.Admin => "Yönetici",
            UserRole.Secretary => "Sekreter",
            UserRole.Host => "Görüşmeci",
            _ => "Kullanıcı"
        };

        LastRefreshedText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    }

    [RelayCommand]
    private void Navigate(string key)
        => WeakReferenceMessenger.Default.Send(new NavigateMessage(key));

    partial void OnTotalCategoryCountChanged(int value)
        => OnPropertyChanged(nameof(CategorySummary));
}
