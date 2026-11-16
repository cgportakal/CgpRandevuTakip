using System.Windows;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class SeriesManageWindow : Window
{
    private readonly SeriesManageViewModel _viewModel;

    public SeriesManageWindow(SeriesManageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Saved += OnSaved;
        _viewModel.Cancelled += OnCancelled;
    }

    public Task InitializeAsync(AppointmentDetailDto detail)
        => _viewModel.InitializeAsync(detail);

    private void OnSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        _viewModel.Cancelled -= OnCancelled;
        base.OnClosed(e);
    }
}
