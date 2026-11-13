using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.Spikes;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Tests;

public class ListImageRendererTests
{
    [Fact]
    public void RenderDailyList_WithFiftyRows_CompletesQuickly()
    {
        Exception? failure = null;
        var elapsed = TimeSpan.Zero;
        var pixelWidth = 0;

        var thread = new Thread(() =>
        {
            try
            {
                var rows = Enumerable.Range(0, 50)
                    .Select(index => new AppointmentListItemDto(
                        Guid.NewGuid().ToString("N"),
                        DateTime.Today.AddHours(9).AddMinutes(index * 15),
                        DateTime.Today.AddHours(9).AddMinutes(index * 15 + 30),
                        30,
                        "Veli",
                        $"Kişi {index}",
                        "+905550000000",
                        "host",
                        "Görüşmeci",
                        "category",
                        "Veli Görüşmesi",
                        "#2E7D5B",
                        AppointmentMethod.FaceToFace,
                        AppointmentStatus.Scheduled,
                        false,
                        false,
                        null))
                    .ToList();

                var stopwatch = Stopwatch.StartNew();
                BitmapSource bitmap = ListImageRenderer.RenderDailyList(
                    "BEK Okulları",
                    DateTime.Today,
                    rows,
                    logoPath: null);
                stopwatch.Stop();

                elapsed = stopwatch.Elapsed;
                pixelWidth = bitmap.PixelWidth;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        Assert.Null(failure);
        Assert.True(pixelWidth > 0);
        Assert.True(
            elapsed < TimeSpan.FromSeconds(5),
            $"PNG üretimi beklenenden yavaş: {elapsed.TotalMilliseconds:N0} ms");
    }

    [Fact]
    public void RenderDailyList_MissingLogoFile_StillRenders()
    {
        Exception? failure = null;
        var pixelHeight = 0;

        var thread = new Thread(() =>
        {
            try
            {
                var bitmap = ListImageRenderer.RenderDailyList(
                    "BEK Okulları",
                    DateTime.Today,
                    Array.Empty<AppointmentListItemDto>(),
                    Path.Combine(Path.GetTempPath(), "olmayan-logo.png"));

                pixelHeight = bitmap.PixelHeight;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        Assert.Null(failure);
        Assert.True(pixelHeight > 0);
    }
}
