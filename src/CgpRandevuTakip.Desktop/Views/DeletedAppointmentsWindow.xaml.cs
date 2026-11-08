using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class DeletedAppointmentsWindow : Window
{
    private readonly DeletedAppointmentsViewModel _viewModel;

    public DeletedAppointmentsWindow(DeletedAppointmentsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Restored += OnRestored;
        _viewModel.CloseRequested += OnCloseRequested;
    }

    public bool RestoredAny { get; private set; }

    public Task InitializeAsync() => _viewModel.LoadAsync();

    private void OnRestored(object? sender, EventArgs e) => RestoredAny = true;

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = RestoredAny;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Restored -= OnRestored;
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}
