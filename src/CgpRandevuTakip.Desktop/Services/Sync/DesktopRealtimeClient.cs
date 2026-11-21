using CgpRandevuTakip.Desktop.Services.Api;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.Services.Sync;

public sealed record AppointmentChangedRealtimeEvent(
    string AppointmentId,
    string Change,
    string DateKey,
    DateTime StartsAtLocal,
    DateTime AtUtc);

public sealed record SeriesChangedRealtimeEvent(
    string SeriesId,
    string Change,
    DateTime AtUtc);

public sealed record ContentChangedRealtimeEvent(
    string Version,
    string Action,
    DateTime AtUtc);

/// <summary>
/// Sunucudaki <c>/hubs/events</c> kanalına bağlanır. Bağlantı kapanınca üstel
/// geri çekilmeyle (2 → 30 sn, süresiz) yeniden dener.
/// </summary>
public sealed class DesktopRealtimeClient : IAsyncDisposable
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private readonly ApiConnectionService _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private HubConnection? _hub;
    private CancellationTokenSource? _lifetime;
    private int _attempts;
    private bool _stopping;

    public DesktopRealtimeClient(ApiConnectionService connection)
    {
        _connection = connection;
    }

    public event EventHandler<AppointmentChangedRealtimeEvent>? AppointmentChanged;

    public event EventHandler<SeriesChangedRealtimeEvent>? SeriesChanged;

    public event EventHandler<ContentChangedRealtimeEvent>? ContentChanged;

    public event EventHandler? ConnectionStateChanged;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public string? LastError { get; private set; }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _stopping = false;

            if (_hub is not null
                && _hub.State is HubConnectionState.Connected
                    or HubConnectionState.Connecting
                    or HubConnectionState.Reconnecting)
            {
                return;
            }

            var session = await _connection.GetSessionAsync();
            if (session is null)
                return;

            _lifetime ??= new CancellationTokenSource();
            await StartHubAsync(session.BaseUrl, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _stopping = true;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;

            var hub = _hub;
            _hub = null;

            if (hub is not null)
            {
                try
                {
                    await hub.StopAsync();
                }
                catch
                {
                    // Kapanış hatası kullanıcıyı engellemez.
                }

                await hub.DisposeAsync();
            }
        }
        finally
        {
            _gate.Release();
        }

        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _gate.Dispose();
    }

    private async Task StartHubAsync(string baseUrl, CancellationToken ct)
    {
        var hub = BuildConnection(baseUrl);
        var previous = _hub;
        _hub = hub;

        if (previous is not null)
        {
            try
            {
                await previous.DisposeAsync();
            }
            catch
            {
                // Eski bağlantı zaten kapanmış olabilir.
            }
        }

        try
        {
            await hub.StartAsync(ct);
            _attempts = 0;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _ = ScheduleReconnectAsync();
        }

        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private HubConnection BuildConnection(string baseUrl)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                ApiSessionStore.NormalizeBaseUrl(baseUrl) + "/hubs/events",
                options =>
                {
                    options.AccessTokenProvider = async () =>
                        (await _connection.GetAuthorizedSessionAsync())?.AccessToken;
                })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        connection.On<AppointmentChangedRealtimeEvent>(
            "appointmentChanged",
            payload => AppointmentChanged?.Invoke(this, payload));
        connection.On<SeriesChangedRealtimeEvent>(
            "seriesChanged",
            payload => SeriesChanged?.Invoke(this, payload));
        connection.On<ContentChangedRealtimeEvent>(
            "contentChanged",
            payload => ContentChanged?.Invoke(this, payload));

        connection.Closed += closedException =>
        {
            _ = closedException;
            if (!ReferenceEquals(_hub, connection) || _stopping)
                return Task.CompletedTask;

            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            _ = ScheduleReconnectAsync();
            return Task.CompletedTask;
        };

        return connection;
    }

    private async Task ScheduleReconnectAsync()
    {
        if (_stopping)
            return;

        _lifetime ??= new CancellationTokenSource();
        var token = _lifetime.Token;

        var delay = TimeSpan.FromSeconds(
            Math.Min(MaxDelay.TotalSeconds, 2 * Math.Pow(2, Math.Min(_attempts, 4))));
        _attempts++;

        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_stopping || token.IsCancellationRequested)
            return;

        try
        {
            await _gate.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (_stopping)
                return;

            var session = await _connection.GetSessionAsync();
            if (session is null)
                return;

            await StartHubAsync(session.BaseUrl, token);
        }
        finally
        {
            _gate.Release();
        }
    }
}
