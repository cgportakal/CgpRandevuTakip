using System.Windows;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Desktop.ViewModels;
using CgpRandevuTakip.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly SessionLockService _sessionLock;
    private readonly IServiceProvider _services;
    private bool _lockDialogOpen;

    public MainWindow(
        ShellViewModel viewModel,
        SessionLockService sessionLock,
        IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _sessionLock = sessionLock;
        _services = services;
        DataContext = viewModel;

        viewModel.LogoutRequested += OnLogoutRequested;
        _sessionLock.LockRequested += OnLockRequested;
        Loaded += OnLoaded;
    }

    public bool LogoutRequested { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
        _sessionLock.Start();
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
            var lockWindow = _services.GetRequiredService<LockWindow>();
            lockWindow.Owner = this;
            lockWindow.ShowDialog();

            if (lockWindow.LogoutRequested)
            {
                LogoutRequested = true;
                Close();
                return;
            }

            _sessionLock.Start();
        }
        finally
        {
            _lockDialogOpen = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LogoutRequested -= OnLogoutRequested;
        _sessionLock.LockRequested -= OnLockRequested;
        _sessionLock.Stop();
        _sessionLock.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
