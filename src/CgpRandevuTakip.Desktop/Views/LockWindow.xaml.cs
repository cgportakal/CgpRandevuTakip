using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class LockWindow : Window
{
    private readonly LockViewModel _viewModel;
    private bool _allowClose;

    public LockWindow(LockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Unlocked += OnUnlocked;
        _viewModel.LogoutRequested += OnLogoutRequested;

        Loaded += (_, _) => PasswordBox.Focus();
    }

    public bool LogoutRequested { get; private set; }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Password = PasswordBox.Password;

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (PasswordBox.Password.Length > 0)
            _viewModel.Password = PasswordBox.Password;

        if (_viewModel.UnlockCommand.CanExecute(null))
            _viewModel.UnlockCommand.Execute(null);
    }

    private void OnUnlocked(object? sender, EventArgs e)
    {
        _allowClose = true;
        DialogResult = true;
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        LogoutRequested = true;
        _allowClose = true;
        DialogResult = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
            e.Cancel = true;

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Unlocked -= OnUnlocked;
        _viewModel.LogoutRequested -= OnLogoutRequested;
        base.OnClosed(e);
    }
}
