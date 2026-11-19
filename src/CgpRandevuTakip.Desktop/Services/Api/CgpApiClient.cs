using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CgpRandevuTakip.Desktop.Services.Api;

/// <summary>
/// API ile konuşan ince HTTP istemcisi. İş kuralı içermez; yalnızca istek/yanıt
/// eşlemesi ve okunabilir hata mesajı üretir.
/// </summary>
public sealed class CgpApiClient
{
    private const string DeviceName = "CGP-Desktop";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public CgpApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiResult<ApiHealth>> GetHealthAsync(string baseUrl, CancellationToken ct = default)
        => SendAsync<ApiHealth>(new HttpRequestMessage(HttpMethod.Get, Url(baseUrl, "/v1/health")), null, ct);

    public Task<ApiResult<ApiAuthTokens>> LoginAsync(
        string baseUrl,
        string username,
        string password,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url(baseUrl, "/v1/auth/login"))
        {
            Content = JsonContent.Create(new { username, password, deviceName = DeviceName }, options: JsonOptions),
        };
        return SendAsync<ApiAuthTokens>(request, null, ct);
    }

    public Task<ApiResult<ApiAuthTokens>> RefreshAsync(
        string baseUrl,
        string refreshToken,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url(baseUrl, "/v1/auth/refresh"))
        {
            Content = JsonContent.Create(new { refreshToken }, options: JsonOptions),
        };
        return SendAsync<ApiAuthTokens>(request, null, ct);
    }

    public async Task LogoutAsync(string baseUrl, string refreshToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url(baseUrl, "/v1/auth/logout"))
        {
            Content = JsonContent.Create(new { refreshToken }, options: JsonOptions),
        };

        try
        {
            using var response = await _http.SendAsync(request, ct);
            _ = response.IsSuccessStatusCode;
        }
        catch
        {
            // Çıkışta ağ hatası kullanıcıyı engellemez; yerel token'lar yine silinir.
        }
    }

    public Task<ApiResult<ApiUser>> GetMeAsync(
        string baseUrl,
        string accessToken,
        CancellationToken ct = default)
        => SendAsync<ApiUser>(new HttpRequestMessage(HttpMethod.Get, Url(baseUrl, "/v1/auth/me")), accessToken, ct);

    private static string Url(string baseUrl, string path)
        => ApiSessionStore.NormalizeBaseUrl(baseUrl) + path;

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        string? accessToken,
        CancellationToken ct)
    {
        using var _ = request;

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                string? error = null;
                try
                {
                    error = JsonSerializer.Deserialize<ApiError>(body, JsonOptions)?.Error;
                }
                catch (JsonException)
                {
                    // HTML/proxy hata sayfası olabilir.
                }

                return ApiResult<T>.Fail(
                    string.IsNullOrWhiteSpace(error) ? $"Sunucu {statusCode} döndürdü." : error,
                    statusCode);
            }

            if (string.IsNullOrWhiteSpace(body))
                return ApiResult<T>.Fail("Sunucu boş yanıt döndürdü.", statusCode);

            var data = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return data is null
                ? ApiResult<T>.Fail("Sunucu yanıtı okunamadı.", statusCode)
                : ApiResult<T>.Ok(data, statusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ApiResult<T>.Fail("Sunucu zaman aşımına uğradı.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Fail($"Sunucuya ulaşılamadı: {ex.Message}");
        }
    }
}
