using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ToastPreviewWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ToastPreviewWindow(string title, string body)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timer.Tick += (_, _) => Close();

        Loaded += OnLoaded;
        Closed += (_, _) => _timer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 20;
        Top = area.Bottom - ActualHeight - 20;

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));

        try
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch
        {
            // Ses aygıtı yoksa önizleme sessiz devam eder.
        }

        _timer.Start();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
