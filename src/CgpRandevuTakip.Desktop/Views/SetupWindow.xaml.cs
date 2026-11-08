using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class SetupWindow : Window
{
    private readonly SetupViewModel _viewModel;

    public SetupWindow(SetupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.SetupSucceeded += OnSetupSucceeded;
        Loaded += (_, _) => UsernameBox.Focus();
        Closed += (_, _) => viewModel.SetupSucceeded -= OnSetupSucceeded;
    }

    private void OnSetupSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Password = PasswordBox.Password;

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
}
