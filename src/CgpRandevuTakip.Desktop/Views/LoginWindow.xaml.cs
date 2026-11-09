using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CgpRandevuTakip.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private readonly IServiceProvider _services;

    public LoginWindow(LoginViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        DataContext = viewModel;

        if (!string.IsNullOrEmpty(viewModel.Password))
            PasswordBox.Password = viewModel.Password;

        viewModel.LoginSucceeded += OnLoginSucceeded;
        viewModel.ForgotPasswordRequested += OnForgotPasswordRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += (_, _) => UsernameBox.Focus();
        Closed += (_, _) =>
        {
            viewModel.LoginSucceeded -= OnLoginSucceeded;
            viewModel.ForgotPasswordRequested -= OnForgotPasswordRequested;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
    }

    private void OnForgotPasswordRequested(object? sender, EventArgs e)
    {
        var window = _services.GetRequiredService<ForgotPasswordWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (PasswordBox.Password.Length > 0)
            _viewModel.Password = PasswordBox.Password;

        if (_viewModel.LoginCommand.CanExecute(null))
            _viewModel.LoginCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LoginViewModel.Password))
            return;

        if (string.IsNullOrEmpty(_viewModel.Password))
        {
            if (PasswordBox.Password.Length > 0)
                PasswordBox.Clear();
        }
        else if (PasswordBox.Password != _viewModel.Password)
        {
            PasswordBox.Password = _viewModel.Password;
        }
    }
}
