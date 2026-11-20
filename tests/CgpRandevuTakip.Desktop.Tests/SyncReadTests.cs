using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Desktop.Services.Sync;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Tests;

public class SyncReadTests
{
    private static readonly JsonSerializerOptions JsonOptions = ApiJson.Options;

    [Fact]
    public async Task GetDayAsync_WhenServerResponds_CachesRows()
    {
        var cache = new InMemorySyncCache();
        var reads = CreateReads(cache, (request, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, new[] { Row("a1", "Ayşe Yılmaz") })));

        var first = await reads.GetDayAsync(new DateTime(2026, 11, 20));
        var second = await reads.GetDayAsync(new DateTime(2026, 11, 20));

        Assert.True(first.Success);
        Assert.Single(first.Data!);
        Assert.True(second.Success);
        Assert.Single(second.Data!);
        Assert.False(reads.IsOffline);
        Assert.NotNull(await cache.GetAsync("day:2026-11-20"));
    }

    [Fact]
    public async Task GetDayAsync_WhenServerUnreachable_ReturnsCachedRows()
    {
        var cache = new InMemorySyncCache();
        var online = true;
        var reads = CreateReads(cache, (_, _) => online
            ? Task.FromResult(Json(HttpStatusCode.OK, new[] { Row("a1", "Ayşe Yılmaz") }))
            : throw new HttpRequestException("ağ yok"));

        var warm = await reads.GetDayAsync(new DateTime(2026, 11, 20));
        Assert.True(warm.Success);
        Assert.False(reads.IsOffline);

        online = false;
        var result = await reads.GetDayAsync(new DateTime(2026, 11, 20));

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.True(reads.IsOffline);

        // Ağ hatasından sonra kısa süre sunucu hiç denenmez; önbellek anında döner.
        var cached = await reads.GetDayAsync(new DateTime(2026, 11, 20));
        Assert.True(cached.Success);
        Assert.Single(cached.Data!);
    }

    [Fact]
    public async Task GetManifestAsync_WhenNotModified_ReturnsCachedManifest()
    {
        var cache = new InMemorySyncCache();
        var manifest = Manifest();

        var calls = 0;
        string? ifNoneMatch = null;
        var reads = CreateReads(cache, (request, _) =>
        {
            calls++;
            if (calls == 1)
                return Task.FromResult(Json(HttpStatusCode.OK, manifest, "\"v1\""));

            ifNoneMatch = request.Headers.TryGetValues("If-None-Match", out var values)
                ? values.Single()
                : null;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        });

        var first = await reads.GetManifestAsync();
        var second = await reads.GetManifestAsync();

        Assert.Equal("\"v1\"", first.ETag);
        Assert.Equal("\"v1\"", ifNoneMatch);
        Assert.True(second.Success);
        Assert.Equal("BEK Okulları", second.Data!.Institution.Name);
        Assert.Equal(2, second.Data.Categories.Count);
    }

    [Fact]
    public async Task SearchAsync_SendsFiltersInQuery()
    {
        string? query = null;
        var reads = CreateReads(new InMemorySyncCache(), (request, _) =>
        {
            query = request.RequestUri!.Query;
            return Task.FromResult(Json(HttpStatusCode.OK, Array.Empty<object>()));
        });

        await reads.SearchAsync("ayşe", "cat-1", "Scheduled", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), "host-1");

        Assert.Contains("search=ay%C5%9Fe", query);
        Assert.Contains("categoryId=cat-1", query);
        Assert.Contains("status=Scheduled", query);
        Assert.Contains("from=2026-11-01", query);
        Assert.Contains("to=2026-11-30", query);
        Assert.Contains("hostUserId=host-1", query);
    }

    [Fact]
    public async Task SyncedAppointmentService_WithoutSession_UsesLocalService()
    {
        var local = new FakeLocalAppointmentService();
        var reads = CreateReads(new InMemorySyncCache(), (_, _) =>
            throw new InvalidOperationException("Sunucu çağrılmamalı."), withSession: false);

        var service = new SyncedAppointmentService(local, reads);
        var rows = await service.GetDayAsync(DateTime.Today);

        Assert.Single(rows);
        Assert.Equal("Yerel", rows[0].PersonFirstName);
    }

    [Fact]
    public async Task SyncedAppointmentService_WhenReadModeOff_UsesLocalEvenWithSession()
    {
        var local = new FakeLocalAppointmentService();
        var reads = CreateReads(
            new InMemorySyncCache(),
            (_, _) => throw new InvalidOperationException("Okuma modu kapalıyken sunucu çağrılmamalı."),
            withSession: true,
            readMode: false);

        var service = new SyncedAppointmentService(local, reads);
        var rows = await service.GetDayAsync(DateTime.Today);

        Assert.Single(rows);
        Assert.Equal("Yerel", rows[0].PersonFirstName);
    }

    [Fact]
    public async Task SyncedAppointmentService_WithSession_UsesServerRows()
    {        var local = new FakeLocalAppointmentService();
        var reads = CreateReads(
            new InMemorySyncCache(),
            (_, _) => Task.FromResult(Json(HttpStatusCode.OK, new[]
            {
                Row("s1", "Sunucu Bir"),
                Row("s2", "Sunucu İki"),
            })));

        var service = new SyncedAppointmentService(local, reads);
        var rows = await service.GetDayAsync(DateTime.Today);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Sunucu Bir", rows[0].PersonFirstName);
    }

    [Fact]
    public async Task SyncedCategoryService_UsesManifestCategories()
    {
        var manifest = Manifest();
        var reads = CreateReads(
            new InMemorySyncCache(),
            (_, _) => Task.FromResult(Json(HttpStatusCode.OK, manifest, "\"v1\"")));

        var service = new SyncedCategoryService(new FakeLocalCategoryService(), reads);
        var categories = await service.GetAllAsync();

        Assert.Equal(2, categories.Count);
        Assert.Equal("Veli Görüşmesi", categories[0].Name);
    }

    private static ServerReadService CreateReads(
        ISyncCacheStore cache,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        bool withSession = true,
        bool readMode = true)
    {
        var settings = new FakeSettingsRepository();
        settings.SetManyAsync(new Dictionary<string, string>
        {
            [ServerReadService.ReadModeSettingKey] = readMode ? "1" : "0",
        }).GetAwaiter().GetResult();

        var store = new ApiSessionStore(settings, new FakeProtector());
        if (withSession)
        {
            store.SaveAsync(new ApiSession(
                "http://localhost:5199",
                "access",
                "refresh",
                DateTimeOffset.UtcNow.AddHours(1),
                new ApiUser("u1", "admin", "Yönetici", "Admin"))).GetAwaiter().GetResult();
        }

        var http = new HttpClient(new StubHandler(handler));
        var client = new CgpApiClient(http);
        var connection = new ApiConnectionService(client, store);
        return new ServerReadService(client, connection, cache, settings);
    }

    private static AppointmentListItemDto Row(string id, string first) => new(
        id,
        DateTime.Today.AddHours(9),
        DateTime.Today.AddHours(9.5),
        30,
        first,
        "Test",
        "+905550000000",
        "host-1",
        "Görüşmeci",
        "cat-1",
        "Veli Görüşmesi",
        "#2E7D5B",
        AppointmentMethod.Phone,
        AppointmentStatus.Scheduled,
        false,
        false,
        null);

    private static ApiManifest Manifest() => new(
        "v1",
        DateTime.UtcNow,
        new ApiInstitution("BEK Okulları", "info@bek.local", "+905550000000", "Adres", null),
        new ApiPublicSettings(
            "Light", 30, "09:00", "17:00", false, true, true, false, "22:00", "08:00", "17:30"),
        new List<CategoryDto>
        {
            new("cat-1", "Veli Görüşmesi", "#2E7D5B", 1, true, 5),
            new("cat-2", "Öğrenci Görüşmesi", "#3E8E7E", 2, true, 2),
        },
        new List<HostDto>
        {
            new("host-1", "Görüşmeci", "host", UserRole.Host, false, true, 7),
        },
        new Dictionary<string, bool> { ["reports"] = true },
        new ApiVersionPolicy("1.5.0", "1.0.0", "1.0.0", "1.9.0", "1.0.0", "1.9.0"));

    private static HttpResponseMessage Json(HttpStatusCode status, object body, string? etag = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };

        if (etag is not null)
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);

        return response;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    private sealed class InMemorySyncCache : ISyncCacheStore
    {
        private readonly Dictionary<string, SyncCacheValue> _values = new(StringComparer.Ordinal);

        public Task<SyncCacheValue?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SaveAsync(string key, string payload, string? etag = null, CancellationToken ct = default)
        {
            _values[key] = new SyncCacheValue(payload, etag, DateTime.UtcNow);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken ct = default)
        {
            _values.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync()
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(_values, StringComparer.Ordinal));

        public Task SetManyAsync(IReadOnlyDictionary<string, string> values)
        {
            foreach (var (key, value) in values)
                _values[key] = value;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeProtector : ISecretProtector
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }

    private sealed class FakeLocalCategoryService : ICategoryService
    {
        public Task<IReadOnlyList<CategoryDto>> GetAllAsync()
            => Task.FromResult<IReadOnlyList<CategoryDto>>(new[]
            {
                new CategoryDto("local", "Yerel Kategori", "#000000", 1, true, 0),
            });

        public Task<IReadOnlyList<CategoryDto>> GetActiveAsync() => GetAllAsync();

        public Task<OperationResult> CreateAsync(CategoryInput input) => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> UpdateAsync(string id, CategoryInput input) => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> SetActiveAsync(string id, bool isActive) => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> DeleteAsync(string id) => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> MoveAsync(string id, int direction) => Task.FromResult(OperationResult.Ok());
    }

    /// <summary>Yalnızca okuma testlerinde kullanılan minimal yerel servis.</summary>
    private sealed class FakeLocalAppointmentService : IAppointmentService
    {
        public Task<IReadOnlyList<AppointmentListItemDto>> GetDayAsync(DateTime localDate)
            => Task.FromResult<IReadOnlyList<AppointmentListItemDto>>(new[] { Row("l1", "Yerel") });

        public Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(
            string? searchText,
            string? categoryId,
            AppointmentStatus? status,
            DateTime? fromLocal,
            DateTime? toLocal,
            string? hostUserId = null)
            => GetDayAsync(DateTime.Today);

        public Task<AppointmentDetailDto?> GetDetailAsync(string id) => Task.FromResult<AppointmentDetailDto?>(null);

        public Task<AppointmentListItemDto?> GetNextAppointmentAsync()
            => Task.FromResult<AppointmentListItemDto?>(null);

        public Task<IReadOnlyList<DeletedAppointmentDto>> GetDeletedAsync()
            => Task.FromResult<IReadOnlyList<DeletedAppointmentDto>>(Array.Empty<DeletedAppointmentDto>());

        public Task<AppointmentSaveResult> CreateAsync(AppointmentInput input)
            => Task.FromResult(AppointmentSaveResult.Ok("l1"));

        public Task<SeriesPlanDto> PreviewSeriesAsync(AppointmentInput input, SeriesRequest request)
            => throw new NotSupportedException();

        public Task<SeriesCreateResult> CreateSeriesAsync(AppointmentInput input, SeriesRequest request)
            => throw new NotSupportedException();

        public Task<SeriesCancelResult> CancelSeriesAsync(string seriesId) => throw new NotSupportedException();

        public Task<SeriesAppendPlanDto> PreviewAppendAsync(string seriesId, int count) => throw new NotSupportedException();

        public Task<SeriesAppendResult> AppendSeriesAsync(string seriesId, int count) => throw new NotSupportedException();

        public Task<SeriesUpdateResult> UpdateSeriesAsync(string seriesId, SeriesUpdateInput input)
            => throw new NotSupportedException();

        public Task<AppointmentSaveResult> UpdateAsync(string id, AppointmentInput input)
            => Task.FromResult(AppointmentSaveResult.Ok(id));

        public Task<OperationResult> DeleteAsync(string id) => Task.FromResult(OperationResult.Ok());

        public Task<BulkDeleteResult> DeleteManyAsync(IReadOnlyList<string> ids)
            => Task.FromResult(new BulkDeleteResult(0, Array.Empty<string>()));

        public Task<BulkMoveResult> MoveManyAsync(
            IReadOnlyList<string> ids,
            DateTime newDate,
            TimeSpan? newTime,
            bool ignoreConflicts = false)
            => Task.FromResult(new BulkMoveResult(0, Array.Empty<string>(), Array.Empty<AppointmentConflictDto>(), Array.Empty<string>()));

        public Task<OperationResult> RestoreAsync(string id) => Task.FromResult(OperationResult.Ok());

        public Task<int> PurgeExpiredDeletedAsync(DateTime nowUtc) => Task.FromResult(0);

        public Task<int> AnonymizeOldAppointmentsAsync(int retentionYears = 2) => Task.FromResult(0);
    }
}
