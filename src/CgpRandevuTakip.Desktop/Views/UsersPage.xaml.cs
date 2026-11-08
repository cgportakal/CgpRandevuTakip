using System.Windows;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class UsersPage : UserControl
{
    public UsersPage(UsersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel viewModel)
            viewModel.Password = PasswordBox.Password;
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel viewModel)
            viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
    }

    private void OnResetPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel viewModel)
            viewModel.ResetPassword = ResetPasswordBox.Password;
    }

    private void OnResetConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel viewModel)
            viewModel.ResetConfirmPassword = ResetConfirmPasswordBox.Password;
    }
}
