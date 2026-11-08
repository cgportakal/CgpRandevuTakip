using System.Windows;
using System.Windows.Controls;

namespace CgpRandevuTakip.Desktop.Views;

public partial class ReminderPreviewWindow : Window
{
    public ReminderPreviewWindow()
    {
        InitializeComponent();
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string message })
            return;

        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
        ActionGrid.IsEnabled = false;
    }

    private void OnToastClick(object sender, RoutedEventArgs e)
    {
        new ToastPreviewWindow(
            "Sıradaki görüşme 30 dakika sonra",
            "14:30 · Veli Görüşmesi · Yüz yüze").Show();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
