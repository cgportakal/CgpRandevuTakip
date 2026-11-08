using System.Windows;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class CategoriesPage : UserControl
{
    public CategoriesPage(CategoriesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CategoriesViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
