using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class AppointmentOutcomeWindow : Window
{
    private readonly AppointmentOutcomeViewModel _viewModel;

    public AppointmentOutcomeWindow(AppointmentOutcomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Saved += OnSaved;
        _viewModel.Cancelled += OnCancelled;
    }

    public Task InitializeAsync(string appointmentId)
        => _viewModel.InitializeAsync(appointmentId);

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
