using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Reports;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Services.Api;

/// <summary>
/// API ile konuşan ince HTTP istemcisi. İş kuralı içermez; yalnızca istek/yanıt
/// eşlemesi ve okunabilir hata mesajı üretir.
/// </summary>
public sealed class CgpApiClient
{
    private const string DeviceName = "CGP-Desktop";

    private static readonly JsonSerializerOptions JsonOptions = ApiJson.Options;

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

    /// <summary>İçerik manifesti; <paramref name="etag"/> verilirse koşullu istek yapılır.</summary>
    public Task<ApiResult<ApiManifest>> GetManifestAsync(
        string baseUrl,
        string accessToken,
        string? etag = null,
        CancellationToken ct = default)
        => SendAsync<ApiManifest>(
            new HttpRequestMessage(HttpMethod.Get, Url(baseUrl, "/v1/content/manifest")),
            accessToken,
            ct,
            etag);

    public Task<ApiResult<IReadOnlyList<AppointmentListItemDto>>> GetDayAsync(
        string baseUrl,
        string accessToken,
        DateTime localDate,
        CancellationToken ct = default)
        => SendAsync<IReadOnlyList<AppointmentListItemDto>>(
            new HttpRequestMessage(
                HttpMethod.Get,
                Url(baseUrl, "/v1/appointments/day" + Query(("date", localDate.ToString("yyyy-MM-dd"))))),
            accessToken,
            ct);

    public Task<ApiResult<IReadOnlyList<AppointmentListItemDto>>> SearchAppointmentsAsync(
        string baseUrl,
        string accessToken,
        string? searchText,
        string? categoryId,
        string? status,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId,
        CancellationToken ct = default)
        => SendAsync<IReadOnlyList<AppointmentListItemDto>>(
            new HttpRequestMessage(
                HttpMethod.Get,
                Url(baseUrl, "/v1/appointments/search" + Query(
                    ("search", searchText),
                    ("categoryId", categoryId),
                    ("status", status),
                    ("from", fromLocal?.ToString("yyyy-MM-dd")),
                    ("to", toLocal?.ToString("yyyy-MM-dd")),
                    ("hostUserId", hostUserId)))),
            accessToken,
            ct);

    public Task<ApiResult<AppointmentListItemDto>> GetNextAppointmentAsync(
        string baseUrl,
        string accessToken,
        CancellationToken ct = default)
        => SendAsync<AppointmentListItemDto>(
            new HttpRequestMessage(HttpMethod.Get, Url(baseUrl, "/v1/appointments/next")),
            accessToken,
            ct);

    public Task<ApiResult<AppointmentDetailDto>> GetAppointmentAsync(
        string baseUrl,
        string accessToken,
        string id,
        CancellationToken ct = default)
        => SendAsync<AppointmentDetailDto>(
            new HttpRequestMessage(
                HttpMethod.Get,
                Url(baseUrl, "/v1/appointments/" + Uri.EscapeDataString(id))),
            accessToken,
            ct);

    public Task<ApiResult<IReadOnlyList<DeletedAppointmentDto>>> GetDeletedAppointmentsAsync(
        string baseUrl,
        string accessToken,
        CancellationToken ct = default)
        => SendAsync<IReadOnlyList<DeletedAppointmentDto>>(
            new HttpRequestMessage(HttpMethod.Get, Url(baseUrl, "/v1/appointments/deleted")),
            accessToken,
            ct);

    public Task<ApiResult<AppointmentSaveResult>> CreateAppointmentAsync(
        string baseUrl,
        string accessToken,
        AppointmentInput input,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url(baseUrl, "/v1/appointments"))
        {
            Content = JsonContent.Create(input, options: ApiJson.Options),
        };

        return SendAsync<AppointmentSaveResult>(request, accessToken, ct);
    }

    public Task<ApiResult<AppointmentSaveResult>> UpdateAppointmentAsync(
        string baseUrl,
        string accessToken,
        string id,
        AppointmentInput input,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            Url(baseUrl, "/v1/appointments/" + Uri.EscapeDataString(id)))
        {
            Content = JsonContent.Create(input, options: ApiJson.Options),
        };

        return SendAsync<AppointmentSaveResult>(request, accessToken, ct);
    }

    public Task<ApiResult<bool>> DeleteAppointmentAsync(
        string baseUrl,
        string accessToken,
        string id,
        CancellationToken ct = default)
        => SendAsync<bool>(
            new HttpRequestMessage(
                HttpMethod.Delete,
                Url(baseUrl, "/v1/appointments/" + Uri.EscapeDataString(id))),
            accessToken,
            ct);

    public Task<ApiResult<bool>> RestoreAppointmentAsync(
        string baseUrl,
        string accessToken,
        string id,
        CancellationToken ct = default)
        => SendAsync<bool>(
            new HttpRequestMessage(
                HttpMethod.Post,
                Url(baseUrl, "/v1/appointments/" + Uri.EscapeDataString(id) + "/restore")),
            accessToken,
            ct);

    public Task<ApiResult<bool>> SaveOutcomeAsync(
        string baseUrl,
        string accessToken,
        string id,
        AppointmentOutcomeInput input,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            Url(baseUrl, "/v1/appointments/" + Uri.EscapeDataString(id) + "/outcome"))
        {
            Content = JsonContent.Create(input, options: ApiJson.Options),
        };

        return SendAsync<bool>(request, accessToken, ct);
    }

    public Task<ApiResult<SeriesCreateResult>> CreateSeriesAsync(
        string baseUrl,
        string accessToken,
        AppointmentInput input,
        SeriesFrequency frequency,
        int count,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            Url(baseUrl, "/v1/appointments/series"))
        {
            Content = JsonContent.Create(new { input, frequency, count }, options: ApiJson.Options),
        };

        return SendAsync<SeriesCreateResult>(request, accessToken, ct);
    }

    public Task<ApiResult<SeriesCancelResult>> CancelSeriesAsync(
        string baseUrl,
        string accessToken,
        string seriesId,
        CancellationToken ct = default)
        => SendAsync<SeriesCancelResult>(
            new HttpRequestMessage(
                HttpMethod.Post,
                Url(baseUrl, "/v1/appointments/series/" + Uri.EscapeDataString(seriesId) + "/cancel")),
            accessToken,
            ct);

    public Task<ApiResult<SeriesAppendResult>> AppendSeriesAsync(
        string baseUrl,
        string accessToken,
        string seriesId,
        int count,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            Url(baseUrl, "/v1/appointments/series/" + Uri.EscapeDataString(seriesId) + "/append"))
        {
            Content = JsonContent.Create(new { count }, options: ApiJson.Options),
        };

        return SendAsync<SeriesAppendResult>(request, accessToken, ct);
    }

    public Task<ApiResult<SeriesUpdateResult>> UpdateSeriesAsync(
        string baseUrl,
        string accessToken,
        string seriesId,
        SeriesUpdateInput input,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            Url(baseUrl, "/v1/appointments/series/" + Uri.EscapeDataString(seriesId)))
        {
            Content = JsonContent.Create(input, options: ApiJson.Options),
        };

        return SendAsync<SeriesUpdateResult>(request, accessToken, ct);
    }

    public Task<ApiResult<BulkMoveResult>> MoveAppointmentsAsync(
        string baseUrl,
        string accessToken,
        IReadOnlyList<string> ids,
        DateTime newDate,
        TimeSpan? newTime,
        bool ignoreConflicts,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            Url(baseUrl, "/v1/appointments/bulk-move"))
        {
            Content = JsonContent.Create(
                new { ids, newDate, newTime, ignoreConflicts },
                options: ApiJson.Options),
        };

        return SendAsync<BulkMoveResult>(request, accessToken, ct);
    }

    public Task<ApiResult<ReportSummaryDto>> GetReportSummaryAsync(
        string baseUrl,
        string accessToken,
        ReportQuery query,
        CancellationToken ct = default)
        => SendAsync<ReportSummaryDto>(
            new HttpRequestMessage(
                HttpMethod.Get,
                Url(baseUrl, "/v1/reports/summary" + ReportQueryString(query, personSearch: null))),
            accessToken,
            ct);

    public Task<ApiResult<ReportResultDto>> GetReportAsync(
        string baseUrl,
        string accessToken,
        ReportKind kind,
        ReportQuery query,
        string? personSearch = null,
        CancellationToken ct = default)
        => SendAsync<ReportResultDto>(
            new HttpRequestMessage(
                HttpMethod.Get,
                Url(
                    baseUrl,
                    "/v1/reports/kind/" + Uri.EscapeDataString(kind.ToString())
                    + ReportQueryString(query, personSearch))),
            accessToken,
            ct);

    private static string ReportQueryString(ReportQuery query, string? personSearch)
        => Query(
            ("from", query.FromLocalDate.ToString("yyyy-MM-dd")),
            ("to", query.ToLocalDate.ToString("yyyy-MM-dd")),
            ("categoryId", query.CategoryId),
            ("hostUserId", query.HostUserId),
            ("personSearch", personSearch));

    private static string Query(params (string Key, string? Value)[] parts)
    {
        var pairs = parts
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value!)}")
            .ToList();

        return pairs.Count == 0 ? string.Empty : "?" + string.Join("&", pairs);
    }

    private static string Url(string baseUrl, string path)
        => ApiSessionStore.NormalizeBaseUrl(baseUrl) + path;

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        string? accessToken,
        CancellationToken ct,
        string? etag = null)
    {
        using var _ = request;

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (!string.IsNullOrWhiteSpace(etag))
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var statusCode = (int)response.StatusCode;
            var responseEtag = response.Headers.ETag?.Tag;

            if (response.StatusCode == HttpStatusCode.NotModified)
                return ApiResult<T>.NotModifiedResult(responseEtag ?? etag);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.ResetContent)
                return ApiResult<T>.Empty(statusCode);

            var body = await response.Content.ReadAsStringAsync(ct);

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
                : ApiResult<T>.Ok(data, statusCode, responseEtag);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ApiResult<T>.Fail("Sunucu zaman aşımına uğradı.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Fail($"Sunucuya ulaşılamadı: {ex.Message}");
        }
        catch (JsonException)
        {
            return ApiResult<T>.Fail("Sunucu yanıtı okunamadı.");
        }
    }
}
