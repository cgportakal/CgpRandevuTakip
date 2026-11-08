using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Desktop.ViewModels;
using CgpRandevuTakip.Desktop.Views;
using CgpRandevuTakip.Infrastructure.Auditing;
using CgpRandevuTakip.Infrastructure.Persistence;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Infrastructure.Security;
using CgpRandevuTakip.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ConfigureUiCulture();
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!AcquireSingleInstance())
        {
            MessageBox.Show(
                "CGP - Randevu Takip zaten çalışıyor. Açık pencereyi kullanın.",
                "CGP - Randevu Takip",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        try
        {
            AppPaths.EnsureCreated();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            await InitializeDatabaseAsync();
            await _host.StartAsync();

            await RunApplicationLoopAsync();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);

            MessageBox.Show(
                $"Uygulama başlatılamadı.\n\n{ex.Message}\n\nTeknik günlük: {AppPaths.LogsDirectory}",
                "CGP - Randevu Takip",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new SqliteDbContextFactory(AppPaths.DatabasePath));
        services.AddSingleton<IDbContextFactory<AppDbContext>>(
            sp => sp.GetRequiredService<SqliteDbContextFactory>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAppEnvironment, WindowsAppEnvironment>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuditLogger, AuditLogger>();

        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IAppointmentRepository, AppointmentRepository>();
        services.AddSingleton<IPersonRepository, PersonRepository>();

        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAppointmentService, AppointmentService>();
        services.AddSingleton<IHostService, HostService>();
        services.AddSingleton<IAppointmentOutcomeService, AppointmentOutcomeService>();
        services.AddSingleton<IUserService, UserService>();

        services.AddSingleton<ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISpikeService, SpikeService>();
        services.AddSingleton<IAppointmentDialogService, AppointmentDialogService>();
        services.AddSingleton<IAppointmentOutcomeDialogService, AppointmentOutcomeDialogService>();
        services.AddHostedService<DeletedAppointmentCleanupService>();
        services.AddTransient<SessionLockService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AppointmentListViewModel>();
        services.AddTransient<AppointmentEditorViewModel>();
        services.AddTransient<AppointmentDetailViewModel>();
        services.AddTransient<AppointmentOutcomeViewModel>();
        services.AddTransient<HostsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<LockViewModel>();
        services.AddTransient<DeletedAppointmentsViewModel>();

        services.AddTransient<LoginWindow>();
        services.AddTransient<SetupWindow>();
        services.AddTransient<ChangePasswordWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<CategoriesPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<AppointmentListPage>();
        services.AddTransient<HostsPage>();
        services.AddTransient<UsersPage>();
        services.AddTransient<AppointmentEditorWindow>();
        services.AddTransient<AppointmentDetailWindow>();
        services.AddTransient<AppointmentOutcomeWindow>();
        services.AddTransient<LockWindow>();
        services.AddTransient<DeletedAppointmentsWindow>();
    }

    private async Task InitializeDatabaseAsync()
    {
        if (_host is null)
            return;

        var factory = _host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        DatabaseInitializer.Initialize(db, AppPaths.DatabasePath);

        var theme = _host.Services.GetRequiredService<ThemeService>();
        await theme.InitializeAsync();
    }

    private async Task RunApplicationLoopAsync()
    {
        if (_host is null)
            return;

        var auth = _host.Services.GetRequiredService<IAuthService>();

        while (true)
        {
            if (await auth.IsFirstRunAsync())
            {
                var setup = _host.Services.GetRequiredService<SetupWindow>();
                if (setup.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }
            else
            {
                var login = _host.Services.GetRequiredService<LoginWindow>();
                if (login.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            var shell = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = shell;
            shell.ShowDialog();

            if (!shell.LogoutRequested)
                break;
        }

        Shutdown();
    }

    private bool AcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\CgpRandevuTakip.SingleInstance",
            createdNew: out var createdNew);

        if (createdNew)
            return true;

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private static void ConfigureUiCulture()
    {
        // Ekranlarda tarih her zaman gg/AA/yyyy biçiminde gösterilir (D-14).
        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
        culture.DateTimeFormat.DateSeparator = "/";

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var path = Path.Combine(AppPaths.LogsDirectory, $"crash-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Günlük yazılamazsa kullanıcıya gösterilen mesaj yeterlidir.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            }
            catch
            {
                // Kapanışta hata kullanıcıya gösterilmez.
            }

            _host.Dispose();
        }

        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch
            {
                // Mutex bu iş parçacığına ait değilse yoksay.
            }

            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
