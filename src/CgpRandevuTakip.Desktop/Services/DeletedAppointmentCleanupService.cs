using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// 30 günlük geri alma süresi dolan silinmiş görüşmeleri kalıcı olarak temizler.
/// Uygulama açılışında ve günde birkaç kez çalışır; hata iş akışını durdurmaz.
/// </summary>
public sealed class DeletedAppointmentCleanupService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IAppointmentService _appointments;
    private readonly IClock _clock;

    public DeletedAppointmentCleanupService(
        IAppointmentService appointments,
        IClock clock)
    {
        _appointments = appointments;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await _appointments.PurgeExpiredDeletedAsync(_clock.UtcNow);
            }
            catch
            {
                // Temizlik hatası kullanıcı akışını veya uygulamayı durdurmamalıdır.
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(
        PeriodicTimer timer,
        CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
