using CgpRandevuTakip.Desktop.Services.Sync;

namespace CgpRandevuTakip.Desktop.Services.Api;

/// <summary>
/// Oturum yaşam döngüsünü yönetir: sunucu testi, giriş, token yenileme ve çıkış.
/// P1 aşamasında yalnızca kimlik ve bağlantı uçlarını kullanır.
/// </summary>
public sealed class ApiConnectionService
{
    private readonly CgpApiClient _client;
    private readonly ApiSessionStore _store;
    private readonly ISyncCacheStore? _cache;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public ApiConnectionService(
        CgpApiClient client,
        ApiSessionStore store,
        ISyncCacheStore? cache = null)
    {
        _client = client;
        _store = store;
        _cache = cache;
    }

    public Task<string> GetBaseUrlAsync() => _store.GetBaseUrlAsync();

    public Task<ApiSession?> GetSessionAsync() => _store.LoadAsync();

    public async Task<ApiResult<ApiHealth>> TestAsync(string baseUrl, CancellationToken ct = default)
    {
        string normalized;
        try
        {
            normalized = ApiSessionStore.NormalizeBaseUrl(baseUrl);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<ApiHealth>.Fail(ex.Message);
        }

        var result = await _client.GetHealthAsync(normalized, ct);
        if (result.Success)
            await _store.SetBaseUrlAsync(normalized);

        return result;
    }

    public async Task<ApiResult<ApiUser>> SignInAsync(
        string baseUrl,
        string username,
        string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return ApiResult<ApiUser>.Fail("Kullanıcı adı ve şifre gerekli.");

        string normalized;
        try
        {
            normalized = ApiSessionStore.NormalizeBaseUrl(baseUrl);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<ApiUser>.Fail(ex.Message);
        }

        var result = await _client.LoginAsync(normalized, username.Trim(), password, ct);
        if (!result.Success || result.Data is null)
            return ApiResult<ApiUser>.Fail(result.Error ?? "Sunucuya giriş yapılamadı.", result.StatusCode);

        // Başka bir sunucudan/hesaptan kalan okuma önbelleği gösterilmesin.
        if (_cache is not null)
            await _cache.ClearAsync(ct);

        await SaveAsync(normalized, result.Data);
        return ApiResult<ApiUser>.Ok(result.Data.User);
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        var session = await _store.LoadAsync();
        if (session is not null)
            await _client.LogoutAsync(session.BaseUrl, session.RefreshToken, ct);

        await _store.ClearTokensAsync();

        if (_cache is not null)
            await _cache.ClearAsync(ct);
    }

    /// <summary>
    /// Geçerli bir erişim belirteci içeren oturumu döndürür. Süresi dolmak üzereyse
    /// (veya <paramref name="forceRefresh"/> ise) yeniler; yenilenemezse null döner.
    /// Okuma çağrıları bu yöntemi kullanır.
    /// </summary>
    public async Task<ApiSession?> GetAuthorizedSessionAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var session = await _store.LoadAsync();
        if (session is null)
            return null;

        if (!forceRefresh && !session.IsExpiring(DateTimeOffset.UtcNow))
            return session;

        var refreshed = await RefreshAsync(session, ct, force: forceRefresh);
        return refreshed.Success ? refreshed.Data : null;
    }

    /// <summary>
    /// Kayıtlı oturumu döndürür; süresi dolmak üzereyse yeniler, 401 alırsa bir kez
    /// yenileyip yeniden dener. Başarısız yenilemede token'lar temizlenir.
    /// </summary>
    public async Task<ApiResult<ApiUser>> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var session = await _store.LoadAsync();
        if (session is null)
            return ApiResult<ApiUser>.Fail("Sunucu oturumu yok.");

        if (session.IsExpiring(DateTimeOffset.UtcNow))
        {
            var refreshed = await RefreshAsync(session, ct);
            if (!refreshed.Success || refreshed.Data is null)
                return ApiResult<ApiUser>.Fail(refreshed.Error ?? "Sunucu oturumu yenilenemedi.", refreshed.StatusCode);
            session = refreshed.Data;
        }

        var me = await _client.GetMeAsync(session.BaseUrl, session.AccessToken, ct);
        if (me.Success || !me.IsUnauthorized)
            return me;

        var renewed = await RefreshAsync(session, ct, force: true);
        if (!renewed.Success || renewed.Data is null)
            return ApiResult<ApiUser>.Fail(renewed.Error ?? "Sunucu oturumu yenilenemedi.", renewed.StatusCode);

        return await _client.GetMeAsync(renewed.Data.BaseUrl, renewed.Data.AccessToken, ct);
    }

    private async Task<ApiResult<ApiSession>> RefreshAsync(
        ApiSession session,
        CancellationToken ct,
        bool force = false)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Başka bir çağrı bu arada yenilemiş olabilir (401 yenilemesinde zorunlu).
            var current = await _store.LoadAsync();
            if (!force
                && current is not null
                && !current.IsExpiring(DateTimeOffset.UtcNow))
            {
                return ApiResult<ApiSession>.Ok(current);
            }

            var result = await _client.RefreshAsync(session.BaseUrl, session.RefreshToken, ct);
            if (!result.Success || result.Data is null)
            {
                await _store.ClearTokensAsync();
                return ApiResult<ApiSession>.Fail(result.Error ?? "Sunucu oturumu yenilenemedi.", result.StatusCode);
            }

            var renewed = await SaveAsync(session.BaseUrl, result.Data);
            return ApiResult<ApiSession>.Ok(renewed);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<ApiSession> SaveAsync(string baseUrl, ApiAuthTokens tokens)
    {
        var session = new ApiSession(
            baseUrl,
            tokens.AccessToken,
            tokens.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, tokens.ExpiresInSeconds)),
            tokens.User);

        await _store.SaveAsync(session);
        return session;
    }
}
