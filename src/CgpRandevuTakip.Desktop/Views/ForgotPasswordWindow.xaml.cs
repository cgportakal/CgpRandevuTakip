using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ForgotPasswordWindow : Window
{
    private readonly ForgotPasswordViewModel _viewModel;

    public ForgotPasswordWindow(ForgotPasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.Completed += OnCompleted;
    }

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.NewPassword = NewPasswordBox.Password;

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;

    private void OnCompleted(object? sender, EventArgs e)
        => NewPasswordBox.Clear();

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Completed -= OnCompleted;
        base.OnClosed(e);
    }
}
