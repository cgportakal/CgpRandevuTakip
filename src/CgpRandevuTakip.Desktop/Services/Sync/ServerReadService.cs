using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Reports;
using CgpRandevuTakip.Desktop.Services.Api;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Sunucudan okuma yapar; yanıtları yerel önbelleğe yazar ve ağ hatasında son
/// başarılı yanıtı döndürür. Sunucu oturumu yoksa çağıranlar yerel servise düşer.
/// </summary>
public sealed class ServerReadService
{
    /// <summary>P2 okuma modunun açık/kapalı olduğunu tutan ayar anahtarı.</summary>
    public const string ReadModeSettingKey = "sync.read.enabled";

    private static readonly JsonSerializerOptions JsonOptions = ApiJson.Options;
    private static readonly TimeSpan OfflineWindow = TimeSpan.FromSeconds(30);

    private readonly CgpApiClient _client;
    private readonly ApiConnectionService _connection;
    private readonly ISyncCacheStore _cache;
    private readonly ISettingsRepository _settings;
    private readonly SemaphoreSlim _manifestLock = new(1, 1);

    private DateTime _offlineUntilUtc = DateTime.MinValue;

    public ServerReadService(
        CgpApiClient client,
        ApiConnectionService connection,
        ISyncCacheStore cache,
        ISettingsRepository settings)
    {
        _client = client;
        _connection = connection;
        _cache = cache;
        _settings = settings;
    }

    /// <summary>Son okuma ağ hatasıyla önbellekten geldiyse true.</summary>
    public bool IsOffline { get; private set; }

    public string? LastError { get; private set; }

    public DateTime? LastSyncUtc { get; private set; }

    public async Task<bool> HasSessionAsync()
        => await _connection.GetSessionAsync() is not null;

    /// <summary>Kullanıcı okuma modunu açtı mı?</summary>
    public async Task<bool> IsReadModeEnabledAsync()
    {
        var values = await _settings.GetAllAsync();
        return values.TryGetValue(ReadModeSettingKey, out var value) && value == "1";
    }

    public Task SetReadModeEnabledAsync(bool enabled)
        => _settings.SetManyAsync(new Dictionary<string, string>
        {
            [ReadModeSettingKey] = enabled ? "1" : "0",
        });

    /// <summary>Sunucu oturumu var ve okuma modu açıksa API okumaları kullanılır.</summary>
    public async Task<bool> IsServerReadActiveAsync()
        => await IsReadModeEnabledAsync() && await HasSessionAsync();

    public async Task<ApiResult<ApiManifest>> GetManifestAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        // Kategori ve görüşmeci servisleri manifesti eşzamanlı ister; aynı önbellek
        // anahtarına iki yazma SQLite kilidi/yarışına yol açabilir. Tek sıraya alınır.
        await _manifestLock.WaitAsync(ct);
        try
        {
            return await ReadAsync<ApiManifest>(
                "content:manifest",
                conditional: true,
                (session, etag, token) => _client.GetManifestAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    forceRefresh ? null : etag,
                    token),
                ct);
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    public Task<ApiResult<IReadOnlyList<AppointmentListItemDto>>> GetDayAsync(
        DateTime localDate,
        CancellationToken ct = default)
        => ReadAsync<IReadOnlyList<AppointmentListItemDto>>(
            $"day:{localDate:yyyy-MM-dd}",
            conditional: false,
            (session, _, token) => _client.GetDayAsync(session.BaseUrl, session.AccessToken, localDate, token),
            ct);

    public Task<ApiResult<IReadOnlyList<AppointmentListItemDto>>> SearchAsync(
        string? searchText,
        string? categoryId,
        string? status,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId,
        CancellationToken ct = default)
    {
        var key = "search:" + Hash(
            searchText,
            categoryId,
            status,
            fromLocal?.ToString("yyyy-MM-dd"),
            toLocal?.ToString("yyyy-MM-dd"),
            hostUserId);

        return ReadAsync<IReadOnlyList<AppointmentListItemDto>>(
            key,
            conditional: false,
            (session, _, token) => _client.SearchAppointmentsAsync(
                session.BaseUrl,
                session.AccessToken,
                searchText,
                categoryId,
                status,
                fromLocal,
                toLocal,
                hostUserId,
                token),
            ct);
    }

    public Task<ApiResult<AppointmentListItemDto>> GetNextAsync(CancellationToken ct = default)
        => ReadAsync<AppointmentListItemDto>(
            "next",
            conditional: false,
            (session, _, token) => _client.GetNextAppointmentAsync(session.BaseUrl, session.AccessToken, token),
            ct);

    public Task<ApiResult<AppointmentDetailDto>> GetDetailAsync(string id, CancellationToken ct = default)
        => ReadAsync<AppointmentDetailDto>(
            "detail:" + id,
            conditional: false,
            (session, _, token) => _client.GetAppointmentAsync(session.BaseUrl, session.AccessToken, id, token),
            ct);

    public Task<ApiResult<IReadOnlyList<DeletedAppointmentDto>>> GetDeletedAsync(CancellationToken ct = default)
        => ReadAsync<IReadOnlyList<DeletedAppointmentDto>>(
            "deleted",
            conditional: false,
            (session, _, token) => _client.GetDeletedAppointmentsAsync(session.BaseUrl, session.AccessToken, token),
            ct);

    public Task<ApiResult<ReportSummaryDto>> GetReportSummaryAsync(
        ReportQuery query,
        CancellationToken ct = default)
    {
        var key = "report:summary:" + Hash(
            query.FromLocalDate.ToString("yyyy-MM-dd"),
            query.ToLocalDate.ToString("yyyy-MM-dd"),
            query.CategoryId,
            query.HostUserId);

        return ReadAsync<ReportSummaryDto>(
            key,
            conditional: false,
            (session, _, token) => _client.GetReportSummaryAsync(
                session.BaseUrl,
                session.AccessToken,
                query,
                token),
            ct);
    }

    public Task<ApiResult<ReportResultDto>> GetReportAsync(
        ReportKind kind,
        ReportQuery query,
        string? personSearch = null,
        CancellationToken ct = default)
    {
        var key = "report:" + kind + ":" + Hash(
            query.FromLocalDate.ToString("yyyy-MM-dd"),
            query.ToLocalDate.ToString("yyyy-MM-dd"),
            query.CategoryId,
            query.HostUserId,
            personSearch);

        return ReadAsync<ReportResultDto>(
            key,
            conditional: false,
            (session, _, token) => _client.GetReportAsync(
                session.BaseUrl,
                session.AccessToken,
                kind,
                query,
                personSearch,
                token),
            ct);
    }

    /// <summary>Oturum kapatıldığında önbelleği temizler.</summary>
    public Task ClearCacheAsync(CancellationToken ct = default) => _cache.ClearAsync(ct);

    private async Task<ApiResult<T>> ReadAsync<T>(
        string cacheKey,
        bool conditional,
        Func<ApiSession, string?, CancellationToken, Task<ApiResult<T>>> fetch,
        CancellationToken ct)
    {
        var cached = await _cache.GetAsync(cacheKey, ct);

        if (DateTime.UtcNow < _offlineUntilUtc)
            return FromCacheOrFail<T>(cached, "Sunucuya ulaşılamıyor; önbellek kullanılıyor.");

        var session = await _connection.GetAuthorizedSessionAsync(ct: ct);
        if (session is null)
            return FromCacheOrFail<T>(cached, "Sunucu oturumu yok.");

        var etag = conditional ? cached?.ETag : null;
        var result = await fetch(session, etag, ct);

        if (result.IsUnauthorized)
        {
            var renewed = await _connection.GetAuthorizedSessionAsync(forceRefresh: true, ct: ct);
            if (renewed is not null)
                result = await fetch(renewed, etag, ct);
        }

        if (result.NotModified)
        {
            if (cached is not null && TryDeserialize<T>(cached.Payload, out var cachedData))
            {
                MarkOnline();
                return ApiResult<T>.Ok(cachedData!, 200, cached.ETag);
            }

            // Önbellek kaybolmuşsa koşulsuz yeniden çek.
            result = await fetch(session, null, ct);
        }

        if (result.Success)
        {
            if (result.Data is not null)
            {
                await _cache.SaveAsync(
                    cacheKey,
                    JsonSerializer.Serialize(result.Data, JsonOptions),
                    result.ETag,
                    ct);
            }
            else
            {
                // 204: "sıradaki randevu yok" gibi boş yanıtlar eski önbelleği geçersiz kılar.
                await _cache.RemoveAsync(cacheKey, ct);
            }

            MarkOnline();
            return result;
        }

        if (result.StatusCode == 404)
            await _cache.RemoveAsync(cacheKey, ct);

        if (result.IsNetworkError)
        {
            _offlineUntilUtc = DateTime.UtcNow.Add(OfflineWindow);
            return FromCacheOrFail<T>(cached, result.Error ?? "Sunucuya ulaşılamadı.");
        }

        // Sunucu geçici olarak hata veriyorsa son başarılı yanıt gösterilir.
        if (result.StatusCode >= 500)
            return FromCacheOrFail<T>(cached, result.Error ?? "Sunucu geçici hata döndürdü.");

        return result;
    }

    private ApiResult<T> FromCacheOrFail<T>(SyncCacheValue? cached, string error)
    {
        if (cached is not null && TryDeserialize<T>(cached.Payload, out var data))
        {
            IsOffline = true;
            LastError = error;
            return ApiResult<T>.Ok(data!, 200);
        }

        IsOffline = true;
        LastError = error;
        return ApiResult<T>.Fail(error);
    }

    private void MarkOnline()
    {
        IsOffline = false;
        LastError = null;
        LastSyncUtc = DateTime.UtcNow;
    }

    private static bool TryDeserialize<T>(string payload, out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(payload, JsonOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    private static string Hash(params string?[] parts)
    {
        var text = string.Join("|", parts.Select(x => x ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
