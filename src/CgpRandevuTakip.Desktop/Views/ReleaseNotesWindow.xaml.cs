using System.Diagnostics;
using System.Windows;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ReleaseNotesWindow : Window
{
    private readonly string? _releaseUrl;

    public ReleaseNotesWindow(string version, string notes, string? releaseUrl)
    {
        InitializeComponent();

        _releaseUrl = releaseUrl;
        Title = $"Sürüm notları · {version}";
        VersionText.Text = $"{version} sürümünde neler değişti?";
        NotesBox.Text = notes;
        OpenOnGitHub.Visibility = string.IsNullOrWhiteSpace(releaseUrl)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnOpenGitHub(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_releaseUrl))
            return;

        Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
