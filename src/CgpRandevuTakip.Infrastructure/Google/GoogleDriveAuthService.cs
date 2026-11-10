using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Application.Backups;

namespace CgpRandevuTakip.Infrastructure.Google;

/// <summary>
/// Masaüstü OAuth 2.0 + PKCE akışı. Yerel geri dönüş adresi için TcpListener kullanır;
/// yönetici URL rezervasyonu gerektirmez.
/// </summary>
public sealed class GoogleDriveAuthService : IGoogleDriveAuthService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string Scope =
        "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.email";

    public async Task<GoogleDriveAuthResult> ConnectAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return GoogleDriveAuthResult.Fail("İstemci kimliği ve gizli anahtar zorunludur.");

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var redirectUri = $"http://127.0.0.1:{port}/";
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var state = Base64Url(RandomNumberGenerator.GetBytes(16));

            var authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                $"&scope={Uri.EscapeDataString(Scope)}" +
                "&access_type=offline&prompt=consent" +
                $"&code_challenge={challenge}&code_challenge_method=S256" +
                $"&state={state}";

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            var code = string.Empty;
            var returnedState = string.Empty;
            var error = string.Empty;

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

            using (var client = await listener.AcceptTcpClientAsync(linked.Token))
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true))
            {
                var requestLine = await reader.ReadLineAsync(linked.Token) ?? string.Empty;
                while (!string.IsNullOrWhiteSpace(await reader.ReadLineAsync(linked.Token)))
                {
                    // Başlıklar tüketilir.
                }

                var parts = requestLine.Split(' ');
                if (parts.Length >= 2)
                {
                    var uri = new Uri("http://localhost" + parts[1]);
                    var query = ParseQuery(uri.Query);
                    query.TryGetValue("code", out code);
                    query.TryGetValue("state", out returnedState);
                    query.TryGetValue("error", out error);
                }

                const string html =
                    "<html><body style=\"font-family:Segoe UI;padding:32px\">" +
                    "<h2>Bağlantı tamamlandı</h2><p>Bu pencereyi kapatıp uygulamaya dönebilirsiniz.</p>" +
                    "</body></html>";
                var body = Encoding.UTF8.GetBytes(html);
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, linked.Token);
                await stream.WriteAsync(body, linked.Token);
            }

            if (!string.IsNullOrWhiteSpace(error))
                return GoogleDriveAuthResult.Fail($"Google bağlantısı reddedildi: {error}");

            if (!string.Equals(returnedState, state, StringComparison.Ordinal))
                return GoogleDriveAuthResult.Fail("Güvenlik doğrulaması başarısız (state uyuşmuyor).");

            if (string.IsNullOrWhiteSpace(code))
                return GoogleDriveAuthResult.Fail("Google yetkilendirme kodu alınamadı.");

            using var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier
            });

            using var tokenResponse = await Client.PostAsync(
                "https://oauth2.googleapis.com/token",
                tokenForm,
                linked.Token);
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync(linked.Token);
            if (!tokenResponse.IsSuccessStatusCode)
                return GoogleDriveAuthResult.Fail($"Token alınamadı: {tokenBody}");

            using var tokenJson = JsonDocument.Parse(tokenBody);
            if (!tokenJson.RootElement.TryGetProperty("refresh_token", out var refreshElement))
            {
                return GoogleDriveAuthResult.Fail(
                    "Yenileme token'ı alınamadı. Google hesabı erişimini kaldırıp yeniden deneyin.");
            }

            var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
            var email = await GetEmailAsync(accessToken, linked.Token);

            return GoogleDriveAuthResult.Ok(refreshElement.GetString()!, email ?? string.Empty);
        }
        catch (OperationCanceledException)
        {
            return GoogleDriveAuthResult.Fail("Google bağlantısı zaman aşımına uğradı.");
        }
        catch (Exception ex)
        {
            return GoogleDriveAuthResult.Fail($"Google bağlantısı kurulamadı: {ex.Message}");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string?> GetEmailAsync(string? accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/oauth2/v2/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("email", out var email) ? email.GetString() : null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=');
            if (index < 0)
                continue;

            var key = Uri.UnescapeDataString(pair[..index]);
            var value = Uri.UnescapeDataString(pair[(index + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
