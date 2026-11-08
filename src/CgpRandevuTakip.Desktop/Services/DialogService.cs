using System.Windows;

namespace CgpRandevuTakip.Desktop.Services;

public interface IDialogService
{
    bool Confirm(string message, string title = "Onay");
    void Info(string message, string title = "Bilgi");
    void Error(string message, string title = "Hata");
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title = "Onay")
        => MessageBox.Show(
               Owner,
               message,
               title,
               MessageBoxButton.YesNo,
               MessageBoxImage.Question,
               MessageBoxResult.No) == MessageBoxResult.Yes;

    public void Info(string message, string title = "Bilgi")
        => MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message, string title = "Hata")
        => MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private static Window? Owner
        => System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsVisible && w.IsActive);
}
