using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class BulkMoveWindow : Window
{
    private readonly BulkMoveViewModel _viewModel;

    public BulkMoveWindow(BulkMoveViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Completed += OnCompleted;
        _viewModel.Cancelled += OnCancelled;
    }

    public void Initialize(int count, DateTime initialDate, IReadOnlyList<DateTime> startsAtLocal)
        => _viewModel.Initialize(count, initialDate, startsAtLocal);

    public BulkMoveSelection? Selection => _viewModel.Selection;

    private void OnCompleted(object? sender, EventArgs e)
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
        _viewModel.Completed -= OnCompleted;
        _viewModel.Cancelled -= OnCancelled;
        base.OnClosed(e);
    }
}
