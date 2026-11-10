using System.Windows;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class AuditLogPage : UserControl
{
    public AuditLogPage(AuditLogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AuditLogViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
