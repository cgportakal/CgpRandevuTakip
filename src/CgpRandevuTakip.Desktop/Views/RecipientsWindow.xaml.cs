using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class RecipientsWindow : Window
{
    private readonly RecipientsViewModel _viewModel;

    public RecipientsWindow(RecipientsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.CloseRequested += OnCloseRequested;
        Loaded += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}
