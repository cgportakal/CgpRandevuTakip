using CommunityToolkit.Mvvm.Messaging;
using CgpRandevuTakip.Desktop.Messages;
using Microsoft.Extensions.Hosting;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Sunucu modu açıkken açılış sırasını (oturum → manifest → outbox → günlük liste)
/// yürütür ve 60 saniyede bir sessiz pull yapar. SignalR olayları anlık yenilemeyi
/// zaten yapar; bu servis kopukluk/kaçan olaylar için güvenlik ağıdır.
/// </summary>
public sealed class SyncMaintenanceService : BackgroundService
{
    private static readonly TimeSpan PullInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ManifestInterval = TimeSpan.FromMinutes(5);

    private readonly ServerReadService _reads;
    private readonly OutboxService _outbox;

    private DateTime _lastManifestUtc = DateTime.MinValue;
    private int _pullRunning;

    public SyncMaintenanceService(ServerReadService reads, OutboxService outbox)
    {
        _reads = reads;
        _outbox = outbox;
    }

    /// <summary>Açılışta oturum varsa manifesti tazele, kuyruğu gönder, günü önbelleğe al.</summary>
    public async Task PrepareAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await _reads.IsServerReadActiveAsync())
                return;

            await _reads.GetManifestAsync(ct: ct);
            _lastManifestUtc = DateTime.UtcNow;

            await _outbox.FlushAsync(ct);

            await _reads.GetDayAsync(DateTime.Today, ct);
        }
        catch (OperationCanceledException)
        {
            // Açılış zaman aşımı uygulamayı engellemez; arka plan turu devam eder.
        }
        catch
        {
            // Sunucuya ulaşılamıyorsa uygulama önbellekle açılır.
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(PullInterval);
        do
        {
            await PullOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task PullOnceAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _pullRunning, 1) == 1)
            return;

        try
        {
            if (!await _reads.IsServerReadActiveAsync())
                return;

            await _reads.GetDayAsync(DateTime.Today, ct);

            if (DateTime.UtcNow - _lastManifestUtc >= ManifestInterval)
            {
                await _reads.GetManifestAsync(ct: ct);
                _lastManifestUtc = DateTime.UtcNow;
            }

            if (!_reads.IsOffline)
                WeakReferenceMessenger.Default.Send(new ServerSyncTickMessage());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Kapanış.
        }
        catch
        {
            // Ağ hatası sonraki turda yeniden denenir; ServerReadService çevrimdışı işaretler.
        }
        finally
        {
            Interlocked.Exchange(ref _pullRunning, 0);
        }
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
