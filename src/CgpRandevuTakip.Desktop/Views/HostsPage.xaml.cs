using System.ComponentModel;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class HostsPage : UserControl
{
    private HostsViewModel? _viewModel;

    public HostsPage(HostsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is HostsViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.Password = PasswordBox.Password;
    }

    private void OnConfirmPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName == nameof(HostsViewModel.Password)
            && string.IsNullOrEmpty(_viewModel.Password)
            && PasswordBox.Password.Length > 0)
        {
            PasswordBox.Clear();
        }

        if (e.PropertyName == nameof(HostsViewModel.ConfirmPassword)
            && string.IsNullOrEmpty(_viewModel.ConfirmPassword)
            && ConfirmPasswordBox.Password.Length > 0)
        {
            ConfirmPasswordBox.Clear();
        }
    }
}
