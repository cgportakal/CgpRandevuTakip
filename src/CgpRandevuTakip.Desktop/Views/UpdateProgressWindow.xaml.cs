using System.ComponentModel;
using System.Windows;
using CgpRandevuTakip.Desktop.Services;

namespace CgpRandevuTakip.Desktop.Views;

public partial class UpdateProgressWindow : Window
{
    private readonly IUpdateService _updates;
    private readonly UpdateCheckResult _update;
    private bool _allowClose;

    public UpdateProgressWindow(IUpdateService updates, UpdateCheckResult update)
    {
        InitializeComponent();
        _updates = updates;
        _update = update;

        VersionText.Text = string.IsNullOrWhiteSpace(update.LatestVersion)
            ? "Yeni sürüm hazırlanıyor."
            : $"{update.CurrentVersion} → {update.LatestVersion} sürümüne güncelleniyor.";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var progress = new Progress<UpdateDownloadProgress>(OnProgress);

        try
        {
            var result = await _updates.InstallAsync(_update, progress);
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
    }

    private void OnProgress(UpdateDownloadProgress progress)
    {
        StatusText.Text = progress.PhaseText;

        if (progress.Percent is { } percent)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = percent;
            PercentText.Text = $"{percent:0}%";
        }
        else
        {
            ProgressBar.IsIndeterminate = true;
            PercentText.Text = progress.Phase == UpdateProgressPhase.Downloading ? "—" : "…";
        }

        DetailText.Text = progress.TotalBytes is > 0
            ? $"{FormatBytes(progress.BytesReceived)} / {FormatBytes(progress.TotalBytes.Value)}"
            : progress.BytesReceived > 0
                ? FormatBytes(progress.BytesReceived)
                : string.Empty;
    }

    private void ShowError(string message)
    {
        StatusText.Text = "Güncelleme tamamlanamadı.";
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 0;
        PercentText.Text = string.Empty;
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
        CloseButton.Visibility = Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        DialogResult = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
            e.Cancel = true;

        base.OnClosing(e);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / 1024d / 1024d:0.0} MB";

        if (bytes >= 1024)
            return $"{bytes / 1024d:0} KB";

        return $"{bytes} B";
    }
}
