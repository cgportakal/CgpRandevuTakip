using System.Windows;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class BackupsPage : UserControl
{
    public BackupsPage(BackupsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupsViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
