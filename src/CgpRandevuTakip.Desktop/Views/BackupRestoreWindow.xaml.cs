using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class BackupRestoreWindow : Window
{
    private readonly BackupRestoreViewModel _viewModel;

    public BackupRestoreWindow(BackupRestoreViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Restored += OnRestored;
    }

    private void OnRestored(object? sender, EventArgs e) => DialogResult = true;

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Restored -= OnRestored;
        base.OnClosed(e);
    }
}
