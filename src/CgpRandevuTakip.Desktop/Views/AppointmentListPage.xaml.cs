using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class AppointmentListPage : UserControl
{
    private AppointmentListViewModel? _viewModel;
    private bool _subscribed;
    private bool _syncingSelectAll;

    public AppointmentListPage(AppointmentListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppointmentListViewModel viewModel)
        {
            _viewModel = viewModel;
            if (!_subscribed)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _subscribed = true;
            }

            // Görüşmeci rolünde toplu işlem yok; seçim sütunu gizlenir.
            SelectionColumn.Visibility = viewModel.CanBulkSelect
                ? Visibility.Visible
                : Visibility.Collapsed;

            await viewModel.LoadCommand.ExecuteAsync(null);

            _syncingSelectAll = true;
            try
            {
                SelectAllCheckBox.IsChecked = viewModel.IsAllSelected;
            }
            finally
            {
                _syncingSelectAll = false;
            }
        }
    }

    private void OnSelectAllChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingSelectAll)
            return;

        if (DataContext is AppointmentListViewModel viewModel)
            viewModel.SetSelectAll(SelectAllCheckBox.IsChecked == true);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppointmentListViewModel.IsAllSelected))
            return;

        _syncingSelectAll = true;
        try
        {
            SelectAllCheckBox.IsChecked = _viewModel?.IsAllSelected ?? false;
        }
        finally
        {
            _syncingSelectAll = false;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed || _viewModel is null)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribed = false;
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
            return;

        menu.DataContext = DataContext;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
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
