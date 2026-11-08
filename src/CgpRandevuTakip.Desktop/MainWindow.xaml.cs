using System.ComponentModel;
using System.Windows;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Desktop.ViewModels;
using CgpRandevuTakip.Desktop.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly SessionLockService _sessionLock;
    private readonly SessionStateService _sessionState;
    private readonly TrayService _tray;
    private readonly ISettingsService _settings;
    private readonly IServiceProvider _services;
    private bool _lockDialogOpen;
    private bool _closeToTray = true;

    public MainWindow(
        ShellViewModel viewModel,
        SessionLockService sessionLock,
        SessionStateService sessionState,
        TrayService tray,
        ISettingsService settings,
        IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _sessionLock = sessionLock;
        _sessionState = sessionState;
        _tray = tray;
        _settings = settings;
        _services = services;
        DataContext = viewModel;

        viewModel.LogoutRequested += OnLogoutRequested;
        _sessionLock.LockRequested += OnLockRequested;
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(
            this,
            static (recipient, message) =>
                ((MainWindow)recipient)._closeToTray = message.CloseToTray);
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public bool LogoutRequested { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();

        try
        {
            var settings = await _settings.GetAsync();
            _closeToTray = settings.CloseToTray;
        }
        catch
        {
            _closeToTray = true;
        }

        _sessionLock.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeToTray && !LogoutRequested && !_tray.IsExiting)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            _tray.ShowBalloon(
                "CGP - Randevu Takip",
                "Uygulama arka planda çalışmaya devam ediyor.");
            return;
        }
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        LogoutRequested = true;
        Close();
    }

    private void OnLockRequested(object? sender, EventArgs e)
    {
        if (_lockDialogOpen)
            return;

        _lockDialogOpen = true;
        try
        {
            _sessionState.SetLocked(true);
            var lockWindow = _services.GetRequiredService<LockWindow>();
            lockWindow.Owner = this;
            lockWindow.ShowDialog();

            if (lockWindow.LogoutRequested)
            {
                LogoutRequested = true;
                _sessionState.SetLocked(false);
                Close();
                return;
            }

            _sessionLock.Start();
        }
        finally
        {
            _lockDialogOpen = false;
            _sessionState.SetLocked(false);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LogoutRequested -= OnLogoutRequested;
        _sessionLock.LockRequested -= OnLockRequested;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _sessionLock.Stop();
        _sessionLock.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
