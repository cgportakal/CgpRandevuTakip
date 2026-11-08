using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.Completed += OnCompleted;
        Loaded += (_, _) => CurrentPasswordBox.Focus();
        Closed += (_, _) => viewModel.Completed -= OnCompleted;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        MessageBox.Show(this, "Şifreniz güncellendi.", "Şifre değiştir",
            MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }

    private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.CurrentPassword = CurrentPasswordBox.Password;

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.NewPassword = NewPasswordBox.Password;

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
}
