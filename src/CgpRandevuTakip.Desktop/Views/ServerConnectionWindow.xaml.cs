using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ServerConnectionWindow : Window
{
    private readonly ServerConnectionViewModel _viewModel;

    public ServerConnectionWindow(ServerConnectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
        PasswordBox.Focus();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Password = PasswordBox.Password;
}
