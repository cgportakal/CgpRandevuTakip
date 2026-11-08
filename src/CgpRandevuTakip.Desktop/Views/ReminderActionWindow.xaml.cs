using System.ComponentModel;
using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ReminderActionWindow : Window
{
    private readonly ReminderActionViewModel _viewModel;
    private bool _allowClose;

    public ReminderActionWindow(ReminderActionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.ActionTaken += OnActionTaken;
    }

    private void OnActionTaken(object? sender, EventArgs e)
    {
        _allowClose = true;
        DialogResult = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Kullanıcı aksiyon almadan pencere kapanamaz.
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ActionTaken -= OnActionTaken;
        base.OnClosed(e);
    }
}
