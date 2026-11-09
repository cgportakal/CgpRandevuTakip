using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Notifications;
using CgpRandevuTakip.Application.Recipients;
using CgpRandevuTakip.Application.Reminders;
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
using Microsoft.Toolkit.Uwp.Notifications;

namespace CgpRandevuTakip.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;
    private CancellationTokenSource? _showListenerCts;
    private TrayService? _tray;
    private bool _runLoopActive;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ConfigureUiCulture();
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!AcquireSingleInstance())
        {
            // Mevcut örneğe "göster" sinyali gönderildi; yeni süreç sessizce kapanır.
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

            _tray = _host.Services.GetRequiredService<TrayService>();
            _tray.Initialize();
            _tray.OpenRequested += OnTrayOpenRequested;
            _tray.ExitRequested += OnTrayExitRequested;
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;

            var startHidden = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
            if (!startHidden)
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

    private void OnTrayOpenRequested(object? sender, EventArgs e)
    {
        if (_runLoopActive)
        {
            ShowExistingWindow();
            return;
        }

        _ = RunApplicationLoopAsync();
    }

    private void ShowExistingWindow()
    {
        var window = Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsVisible);

        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            return;
        }

        _tray?.ShowWindow();
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
        => Shutdown();

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
        => Dispatcher.Invoke(() => OnTrayOpenRequested(null, EventArgs.Empty));

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new SqliteDbContextFactory(AppPaths.DatabasePath));
        services.AddSingleton<IDbContextFactory<AppDbContext>>(
            sp => sp.GetRequiredService<SqliteDbContextFactory>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAppEnvironment, WindowsAppEnvironment>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IPushNotifier, PushNotifier>();
        services.AddSingleton<WindowsToastService>();
        services.AddSingleton<SessionStateService>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<AutoStartService>();

        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IAppointmentRepository, AppointmentRepository>();
        services.AddSingleton<IPersonRepository, PersonRepository>();
        services.AddSingleton<IRecipientRepository, RecipientRepository>();
        services.AddSingleton<IShareLogRepository, ShareLogRepository>();

        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPasswordResetService, PasswordResetService>();
        services.AddSingleton<IRememberedLoginStore, RememberedLoginStore>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAppointmentService, AppointmentService>();
        services.AddSingleton<IHostService, HostService>();
        services.AddSingleton<IAppointmentOutcomeService, AppointmentOutcomeService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IRecipientService, RecipientService>();
        services.AddSingleton<IDailyShareService, DailyShareService>();
        services.AddSingleton<IReminderService, ReminderService>();

        services.AddSingleton<ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISpikeService, SpikeService>();
        services.AddSingleton<IAppointmentDialogService, AppointmentDialogService>();
        services.AddSingleton<IAppointmentOutcomeDialogService, AppointmentOutcomeDialogService>();
        services.AddHostedService<DeletedAppointmentCleanupService>();
        services.AddHostedService<ReminderDispatcher>();
        services.AddHostedService<DailyShareScheduler>();
        services.AddTransient<SessionLockService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<ForgotPasswordViewModel>();
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
        services.AddTransient<ShareDailyListViewModel>();
        services.AddTransient<RecipientsViewModel>();

        services.AddTransient<LoginWindow>();
        services.AddTransient<ForgotPasswordWindow>();
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
        services.AddTransient<ShareDailyListWindow>();
        services.AddTransient<RecipientsWindow>();
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
        if (_host is null || _runLoopActive)
            return;

        _runLoopActive = true;
        try
        {
            var auth = _host.Services.GetRequiredService<IAuthService>();
            var allowRememberedLogin = true;

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
                    var remembered = _host.Services.GetRequiredService<IRememberedLoginStore>();
                    LoginResult? rememberedLogin = null;

                    // Otomatik giriş yalnızca uygulama açılışında yapılır.
                    // Kullanıcı çıkış yaptıysa giriş formu (önceden doldurulmuş) gösterilir.
                    if (allowRememberedLogin)
                    {
                        var token = remembered.Load();
                        rememberedLogin = token is null
                            ? null
                            : await auth.LoginWithRememberTokenAsync(token);

                        if (token is not null && rememberedLogin?.Success != true)
                            remembered.Clear();
                    }

                    allowRememberedLogin = false;

                    if (rememberedLogin?.Success != true)
                    {
                        var login = _host.Services.GetRequiredService<LoginWindow>();
                        if (login.ShowDialog() != true)
                        {
                            Shutdown();
                            return;
                        }
                    }
                }

                var shell = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = shell;
                _tray?.AttachWindow(shell);

                var closed = new TaskCompletionSource();
                shell.Closed += (_, _) => closed.TrySetResult();
                shell.Show();
                await closed.Task;

                if (!shell.LogoutRequested)
                    break;
            }

            Shutdown();
        }
        finally
        {
            _runLoopActive = false;
        }
    }

    private bool AcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\CgpRandevuTakip.SingleInstance",
            createdNew: out var createdNew);

        if (createdNew)
        {
            _showEvent = new EventWaitHandle(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: @"Local\CgpRandevuTakip.Show",
                createdNew: out _);

            StartShowListener();
            return true;
        }

        try
        {
            if (EventWaitHandle.TryOpenExisting(@"Local\CgpRandevuTakip.Show", out var showEvent))
            {
                showEvent.Set();
                showEvent.Dispose();
            }
        }
        catch
        {
            // Diğer örnek sinyali alamazsa kullanıcıya mesaj gösterilir.
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private void StartShowListener()
    {
        if (_showEvent is null)
            return;

        var showEvent = _showEvent;
        _showListenerCts = new CancellationTokenSource();
        var token = _showListenerCts.Token;

        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                if (!showEvent.WaitOne(500))
                    continue;

                Dispatcher.Invoke(() => OnTrayOpenRequested(null, EventArgs.Empty));
            }
        }, token);
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
        ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
        _showListenerCts?.Cancel();
        _showEvent?.Set();
        _showEvent?.Dispose();
        _showListenerCts?.Dispose();
        _tray?.Dispose();

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
