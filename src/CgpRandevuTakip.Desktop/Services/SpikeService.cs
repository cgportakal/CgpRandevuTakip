using System.Diagnostics;
using System.IO;
using System.Windows;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Spikes;
using CgpRandevuTakip.Desktop.Views;

namespace CgpRandevuTakip.Desktop.Services;

public sealed record SpikeResult(bool Success, string Message, string? ImagePath = null);

public interface ISpikeService
{
    Task<SpikeResult> PrepareDailyListImageAsync();
    SpikeResult OpenWhatsApp();
    void ShowReminderPreview();
    void ShowToastPreview();
}

/// <summary>S-01 (pano görseli + WhatsApp) ve S-02 (hatırlatıcı/toast önizleme) tanılama araçları.</summary>
public sealed class SpikeService : ISpikeService
{
    private readonly ISettingsService _settings;

    public SpikeService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<SpikeResult> PrepareDailyListImageAsync()
    {
        try
        {
            var settings = await _settings.GetAsync();
            var bitmap = ListImageRenderer.RenderSample(settings.InstitutionName, DateTime.Today);

            var directory = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"gunluk-liste-spike-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            ListImageRenderer.SavePng(bitmap, path);

            if (!TryCopyImage(bitmap))
            {
                return new SpikeResult(
                    false,
                    $"Görsel kaydedildi ancak panoya kopyalanamadı: {path}",
                    path);
            }

            return new SpikeResult(
                true,
                $"Görsel üretildi ve panoya kopyalandı.\nDosya: {path}",
                path);
        }
        catch (Exception ex)
        {
            return new SpikeResult(false, $"Görsel üretilemedi: {ex.Message}");
        }
    }

    public SpikeResult OpenWhatsApp()
    {
        try
        {
            Process.Start(new ProcessStartInfo("whatsapp://") { UseShellExecute = true });
            return new SpikeResult(
                true,
                "WhatsApp açıldı. İlgili sohbete gidip Ctrl+V ile görseli yapıştırın.");
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://web.whatsapp.com")
                {
                    UseShellExecute = true
                });

                return new SpikeResult(
                    true,
                    "WhatsApp Desktop açılamadı; WhatsApp Web tarayıcıda açıldı. Görseli Ctrl+V ile yapıştırın.");
            }
            catch (Exception ex)
            {
                return new SpikeResult(false, $"WhatsApp açılamadı: {ex.Message}");
            }
        }
    }

    public void ShowReminderPreview()
    {
        var window = new ReminderPreviewWindow();
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner && owner != window)
            window.Owner = owner;

        window.Show();
    }

    public void ShowToastPreview()
    {
        var window = new ToastPreviewWindow(
            "Sıradaki görüşme 30 dakika sonra",
            "14:30 · Veli Görüşmesi · Yüz yüze");

        window.Show();
    }

    private static bool TryCopyImage(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetImage(bitmap);
                return true;
            }
            catch
            {
                Thread.Sleep(80);
            }
        }

        return false;
    }
}
