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

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        _busy = true;
        UpdateButton.IsEnabled = false;
        ExitButton.IsEnabled = false;
        ErrorPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "Güncelleme indiriliyor ve hazırlanıyor…";
        StatusText.Visibility = Visibility.Visible;

        try
        {
            var result = await _updates.InstallAsync(_update);
            if (result.Success)
            {
                _allowClose = true;
                DialogResult = true;
                return;
            }

            ShowError(result.Error ?? "Güncelleme başlatılamadı.");
        }
        catch (Exception ex)
        {
            ShowError($"Güncelleme başlatılamadı: {ex.Message}");
        }
        finally
        {
            _busy = false;
            UpdateButton.IsEnabled = true;
            ExitButton.IsEnabled = true;
            StatusText.Visibility = Visibility.Collapsed;
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
