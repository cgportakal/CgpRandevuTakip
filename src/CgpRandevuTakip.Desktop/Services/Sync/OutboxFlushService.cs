using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Outbox kuyruğunu açılışta ve periyodik olarak sunucuya gönderir.
/// </summary>
public sealed class OutboxFlushService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly OutboxService _outbox;

    public OutboxFlushService(OutboxService outbox)
    {
        _outbox = outbox;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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
                if (await _outbox.GetPendingCountAsync(stoppingToken) > 0)
                    await _outbox.FlushAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Gönderim hatası sonraki turda yeniden denenir.
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
