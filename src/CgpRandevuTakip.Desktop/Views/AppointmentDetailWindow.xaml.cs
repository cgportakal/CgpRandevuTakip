using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class AppointmentDetailWindow : Window
{
    private readonly AppointmentDetailViewModel _viewModel;

    public AppointmentDetailWindow(AppointmentDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.EditRequested += OnEditRequested;
        _viewModel.Deleted += OnDeleted;
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.RepeatRequested += OnRepeatRequested;
    }

    public bool EditRequested { get; private set; }

    public bool RepeatRequested { get; private set; }

    public bool NeedsRefresh => _viewModel.NeedsRefresh;

    public Task InitializeAsync(string appointmentId)
        => _viewModel.LoadAsync(appointmentId);

    private void OnEditRequested(object? sender, EventArgs e)
    {
        EditRequested = true;
        DialogResult = true;
        Close();
    }

    private void OnDeleted(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnRepeatRequested(object? sender, EventArgs e)
    {
        RepeatRequested = true;
        DialogResult = true;
        Close();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.EditRequested -= OnEditRequested;
        _viewModel.Deleted -= OnDeleted;
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.RepeatRequested -= OnRepeatRequested;
        base.OnClosed(e);
    }
}
