using CgpRandevuTakip.Application.Appointments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// KVKK saklama politikası: 2 yıldan eski görüşmelerdeki kişisel verileri günlük olarak anonimleştirir.
/// </summary>
public sealed class RetentionService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceProvider _services;

    public RetentionService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var appointments = _services.GetRequiredService<IAppointmentService>();
                await appointments.AnonymizeOldAppointmentsAsync(retentionYears: 2);
            }
            catch
            {
                // Saklama temizliği uygulamayı durdurmaz; sonraki döngüde yeniden denenir.
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
