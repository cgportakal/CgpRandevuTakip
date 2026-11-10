using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Windows Action Center toast gönderimi. Paket/ortam desteklemezse false döner;
/// çağıran taraf uygulama içi topmost toast'a düşer.
/// </summary>
public sealed class WindowsToastService
{
    public bool TryShow(string title, string body, bool silent = false, string? reminderId = null)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(body);

            if (!string.IsNullOrWhiteSpace(reminderId))
                builder.AddArgument("reminderId", reminderId);

            if (silent)
                builder.AddAudio(new ToastAudio { Silent = true });

            var content = builder.GetToastContent();

            ToastNotificationManagerCompat.CreateToastNotifier()
                .Show(new ToastNotification(content.GetXml()));

            return true;
        }
        catch
        {
            return false;
        }
    }
}
