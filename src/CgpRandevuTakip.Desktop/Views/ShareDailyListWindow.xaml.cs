using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ShareDailyListWindow : Window
{
    private readonly ShareDailyListViewModel _viewModel;
    private readonly IServiceProvider _services;

    public ShareDailyListWindow(
        ShareDailyListViewModel viewModel,
        IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        DataContext = viewModel;

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.RecipientsRequested += OnRecipientsRequested;
    }

    public Task InitializeAsync(DateTime date) => _viewModel.LoadAsync(date);

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnRecipientsRequested(object? sender, EventArgs e)
    {
        var window = _services.GetRequiredService<RecipientsWindow>();
        window.Owner = this;
        window.ShowDialog();
        _ = _viewModel.LoadAsync(_viewModel.Date);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.RecipientsRequested -= OnRecipientsRequested;
        base.OnClosed(e);
    }
}
