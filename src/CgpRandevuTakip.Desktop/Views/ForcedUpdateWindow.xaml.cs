using System.ComponentModel;
using System.Windows;
using CgpRandevuTakip.Desktop.Services;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ForcedUpdateWindow : Window
{
    private readonly IUpdateService _updates;
    private readonly UpdateCheckResult _update;
    private bool _allowClose;
    private bool _busy;

    public ForcedUpdateWindow(IUpdateService updates, UpdateCheckResult update)
    {
        InitializeComponent();
        _updates = updates;
        _update = update;

        CurrentVersionText.Text = update.CurrentVersion;
        RequiredVersionText.Text = update.MinVersion ?? update.LatestVersion ?? "—";
        LatestVersionText.Text = update.LatestVersion ?? "—";

        if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
            ReleaseNotesText.Text = update.ReleaseNotes;
        else
            ReleaseNotesText.Text = "Bu sürüm için ayrıntılı not bulunmuyor.";
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        _busy = true;
        UpdateButton.IsEnabled = false;
        ExitButton.IsEnabled = false;
        ErrorPanel.Visibility = Visibility.Collapsed;

        try
        {
            var progress = new UpdateProgressWindow(_updates, _update)
            {
                Owner = this
            };

            if (progress.ShowDialog() == true)
            {
                _allowClose = true;
                DialogResult = true;
                return;
            }

            ShowError("Güncelleme tamamlanamadı. Lütfen tekrar deneyin.");
        }
        finally
        {
            _busy = false;
            UpdateButton.IsEnabled = true;
            ExitButton.IsEnabled = true;
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
            e.Cancel = true;

        base.OnClosing(e);
    }
}
