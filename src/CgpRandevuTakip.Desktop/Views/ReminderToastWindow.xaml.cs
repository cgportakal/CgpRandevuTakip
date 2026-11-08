using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ReminderToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ReminderToastWindow(string title, string body)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;

        Loaded += OnLoaded;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
    }

    public event EventHandler? BodyClicked;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;

        _timer.Start();
    }

    private void OnBodyClick(object sender, MouseButtonEventArgs e)
    {
        _timer.Stop();
        BodyClicked?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Close();
    }

    public void ShowNotification()
    {
        Show();
        Opacity = 1;
    }
}
