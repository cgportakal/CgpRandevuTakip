using System.Windows;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
