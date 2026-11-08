using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class AppointmentEditorWindow : Window
{
    private readonly AppointmentEditorViewModel _viewModel;

    public AppointmentEditorWindow(AppointmentEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Saved += OnSaved;
        _viewModel.Cancelled += OnCancelled;
    }

    public Task InitializeAsync(string? appointmentId, DateTime initialDate)
        => _viewModel.InitializeAsync(appointmentId, initialDate);

    public Task InitializeRepeatAsync(string sourceAppointmentId, DateTime initialDate)
        => _viewModel.InitializeRepeatAsync(sourceAppointmentId, initialDate);

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
