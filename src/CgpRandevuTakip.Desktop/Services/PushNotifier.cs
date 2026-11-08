using System.Net.Http;
using System.Net.Http.Json;
using CgpRandevuTakip.Application.Notifications;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>ntfy ve Telegram push gönderimi. Hata durumunda false döner, iş akışını durdurmaz.</summary>
public sealed class PushNotifier : IPushNotifier
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<bool> SendAsync(PushRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return request.Provider switch
            {
                "ntfy" => await SendNtfyAsync(request, cancellationToken),
                "telegram" => await SendTelegramAsync(request, cancellationToken),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SendNtfyAsync(PushRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
            return false;

        using var message = new HttpRequestMessage(HttpMethod.Post, $"https://ntfy.sh/{request.Topic.Trim()}")
        {
            Content = new StringContent(request.Body)
        };
        message.Headers.TryAddWithoutValidation("Title", request.Title);

        using var response = await Client.SendAsync(message, ct);
        return response.IsSuccessStatusCode;
    }

    private static async Task<bool> SendTelegramAsync(PushRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.ChatId))
            return false;

        using var response = await Client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{request.Token.Trim()}/sendMessage",
            new { chat_id = request.ChatId.Trim(), text = $"{request.Title}\n{request.Body}" },
            cancellationToken: ct);

        return response.IsSuccessStatusCode;
    }
}
