using System.Windows.Controls;
using System.Windows.Input;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class AppointmentListPage : UserControl
{
    public AppointmentListPage(AppointmentListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AppointmentListViewModel viewModel)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AppointmentListViewModel viewModel)
            return;

        if (viewModel.SelectedAppointment is null)
            return;

        if (viewModel.OpenDetailCommand.CanExecute(null))
            viewModel.OpenDetailCommand.Execute(null);
    }
}
