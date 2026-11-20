using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class BackupCreateWindow : Window
{
    private readonly BackupCreateViewModel _viewModel;

    public BackupCreateWindow(BackupCreateViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Created += OnCreated;
    }

    private void OnCreated(object? sender, EventArgs e) => DialogResult = true;

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Created -= OnCreated;
        base.OnClosed(e);
    }
}
