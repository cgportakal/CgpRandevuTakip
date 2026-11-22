using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebPush;

namespace CgpRandevuTakip.Api.Notifications;

public interface IDevicePushSender
{
    Task<int> SendAsync(
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task<int> SendToUserAsync(
        string userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Web Push (VAPID) ve Expo Push (FCM/APNs köprüsü) gönderimi.
/// Hata tek cihazı etkiler; diğer cihazlara gönderim devam eder.
/// </summary>
public sealed class DevicePushSender : IDevicePushSender
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly DeviceStore _devices;
    private readonly VapidKeyProvider _vapid;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevicePushSender> _logger;

    public DevicePushSender(
        DeviceStore devices,
        VapidKeyProvider vapid,
        IConfiguration configuration,
        ILogger<DevicePushSender> logger)
    {
        _devices = devices;
        _vapid = vapid;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<int> SendAsync(
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(null, title, body, data, cancellationToken);

    public Task<int> SendToUserAsync(
        string userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(userId, title, body, data, cancellationToken);

    private async Task<int> SendCoreAsync(
        string? userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        var devices = await _devices.ListAsync(userId, cancellationToken);
        if (devices.Count == 0)
            return 0;

        var sent = 0;
        sent += await SendWebPushAsync(devices, title, body, data, cancellationToken);
        sent += await SendExpoAsync(devices, title, body, data, cancellationToken);

        return sent;
    }

    private async Task<int> SendWebPushAsync(
        IReadOnlyList<DeviceRecord> devices,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        var targets = devices
            .Where(device => device.Platform == "web"
                             && !string.IsNullOrWhiteSpace(device.Endpoint)
                             && !string.IsNullOrWhiteSpace(device.P256dh)
                             && !string.IsNullOrWhiteSpace(device.Auth))
            .ToList();

        if (targets.Count == 0)
            return 0;

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            data = data ?? new Dictionary<string, string>(),
            tag = data?.GetValueOrDefault("appointmentId") ?? "cgp"
        });

        var client = new WebPushClient();
        var sent = 0;

        foreach (var device in targets)
        {
            try
            {
                var subscription = new PushSubscription(device.Endpoint!, device.P256dh!, device.Auth!);
                await client.SendNotificationAsync(subscription, payload, _vapid.Details, cancellationToken);
                sent++;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                await _devices.DeleteByIdAsync(device.Id, cancellationToken);
                _logger.LogInformation("Süresi dolmuş web push aboneliği silindi: {DeviceId}", device.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Web push gönderilemedi: {DeviceId}", device.Id);
            }
        }

        client.Dispose();
        return sent;
    }

    private async Task<int> SendExpoAsync(
        IReadOnlyList<DeviceRecord> devices,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (_configuration.GetValue("Expo:Enabled", true) is false)
            return 0;

        var tokens = devices
            .Where(device => device.Platform is "android" or "ios")
            .Select(device => device.Token)
            .Where(token => !string.IsNullOrWhiteSpace(token) && token!.StartsWith("ExponentPushToken", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        if (tokens.Count == 0)
            return 0;

        var messages = tokens.Select(token => new
        {
            to = token,
            title,
            body,
            sound = "default",
            data = data ?? new Dictionary<string, string>()
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://exp.host/--/api/v2/push/send")
        {
            Content = JsonContent.Create(messages)
        };
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        var accessToken = _configuration["Expo:AccessToken"];
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");

        try
        {
            using var response = await Client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return tokens.Count;

            _logger.LogWarning("Expo push yanıtı başarısız: {Status}", (int)response.StatusCode);
            return 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient zaman aşımı, uygulama kapanışından farklıdır; host'u durdurmamalı.
            _logger.LogWarning("Expo push zaman aşımına uğradı; sonraki turda yeniden denenecek.");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Expo push gönderilemedi.");
            return 0;
        }
    }
}
