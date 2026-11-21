using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Desktop.Services.Sync;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Desktop.Tests;

public class OutboxServiceTests
{
    private readonly string _databasePath;
    private readonly InMemoryCache _cache = new();
    private readonly FakeSettingsRepository _settings = new();

    public OutboxServiceTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            "CgpOutboxTests",
            Guid.NewGuid().ToString("N"),
            "data.db");

        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using var db = AppDbContextFactory.Create(_databasePath);
        db.Database.Migrate();
    }

    [Fact]
    public async Task FlushAsync_SendsCreateAndClearsQueue()
    {
        string? sentBody = null;
        var service = CreateService(async (request, _) =>
        {
            sentBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, new
            {
                success = true,
                appointmentId = "server-1",
                error = (string?)null,
                conflicts = (object?)null,
            });
        });

        await service.EnqueueAsync(
            OutboxOperation.AppointmentCreate,
            "req-1",
            Input("req-1"));

        Assert.Equal(1, await service.GetPendingCountAsync());

        var result = await service.FlushAsync();

        Assert.True(result.Sent >= 1, result.Error);
        Assert.Equal(0, await service.GetPendingCountAsync());
        Assert.Contains("\"clientRequestId\":\"req-1\"", sentBody);
    }

    [Fact]
    public async Task FlushAsync_NetworkError_KeepsItemForRetry()
    {
        var service = CreateService((_, _) => throw new HttpRequestException("ağ yok"));

        await service.EnqueueAsync(
            OutboxOperation.AppointmentCreate,
            "req-2",
            Input("req-2"));

        var result = await service.FlushAsync();

        Assert.True(result.Offline);
        Assert.Equal(1, await service.GetPendingCountAsync());
    }

    [Fact]
    public async Task FlushAsync_ServerRejection_MarksFailed()
    {
        var service = CreateService((_, _) =>
            Task.FromResult(Json(HttpStatusCode.BadRequest, new { error = "Geçersiz istek." })));

        await service.EnqueueAsync(
            OutboxOperation.AppointmentCreate,
            "req-3",
            Input("req-3"));

        var result = await service.FlushAsync();

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, await service.GetPendingCountAsync());

        using var db = AppDbContextFactory.Create(_databasePath);
        var item = await db.SyncOutboxItems.SingleAsync();
        Assert.Equal("failed", item.State);
        Assert.Contains("Geçersiz", item.LastError);
    }

    [Fact]
    public async Task FlushAsync_DeleteOperation_SendsDelete()
    {
        string? method = null;
        string? path = null;

        var service = CreateService((request, _) =>
        {
            method = request.Method.Method;
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        await service.EnqueueAsync(OutboxOperation.AppointmentDelete, "server-9", null);

        var result = await service.FlushAsync();

        Assert.Equal(1, result.Sent);
        Assert.Equal(0, await service.GetPendingCountAsync());
        Assert.Equal("DELETE", method);
        Assert.Equal("/v1/appointments/server-9", path);
    }

    [Fact]
    public async Task SyncedCreate_WhenOnline_SendsToApi()
    {
        var (_, sync, _) = CreateServices((_, _) => Task.FromResult(Json(HttpStatusCode.OK, new
        {
            success = true,
            appointmentId = "server-1",
            error = (string?)null,
            conflicts = (object?)null,
        })));

        var result = await sync.CreateAsync(Input("req-online"));

        Assert.True(result.Success, result.Error);
        Assert.False(result.Queued);
        Assert.Equal("server-1", result.AppointmentId);
    }

    [Fact]
    public async Task SyncedCreate_WhenNetworkFails_QueuesAndReports()
    {
        var (outbox, sync, _) = CreateServices((_, _) => throw new HttpRequestException("ağ yok"));

        var result = await sync.CreateAsync(Input("req-offline"));

        Assert.True(result.Success);
        Assert.True(result.Queued);
        Assert.Equal("req-offline", result.AppointmentId);
        Assert.Equal(1, await outbox.GetPendingCountAsync());
    }

    [Fact]
    public async Task FlushAsync_OutcomeOperation_SendsPut()
    {
        string? path = null;
        var service = CreateService((request, _) =>
        {
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        await service.EnqueueAsync(
            OutboxOperation.AppointmentOutcome,
            "server-7",
            new AppointmentOutcomeInput(Attendance.Arrived, "Özet", true, null, true));

        var result = await service.FlushAsync();

        Assert.Equal(1, result.Sent);
        Assert.Equal("/v1/appointments/server-7/outcome", path);
    }

    [Fact]
    public async Task Overlay_ShowsPendingCreateUpdateAndDelete()
    {
        var (outbox, _, overlay) = CreateServices((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var date = DateTime.Today.AddDays(3);

        await outbox.EnqueueAsync(OutboxOperation.AppointmentCreate, "temp-1", Input("temp-1"));

        var rows = await overlay.ApplyAsync(
            Array.Empty<AppointmentListItemDto>(),
            null,
            date,
            date,
            null,
            null);

        var row = Assert.Single(rows);
        Assert.Equal("temp-1", row.Id);
        Assert.Equal("Kuyruk", row.PersonFirstName);

        var detail = await overlay.ApplyDetailAsync(null, "temp-1");
        Assert.NotNull(detail);
        Assert.Equal("Kuyruk Test", detail!.PersonFullName);

        await Task.Delay(10);
        var updated = Input("temp-1") with { FirstName = "Guncel" };
        await outbox.EnqueueAsync(OutboxOperation.AppointmentUpdate, "temp-1", updated);

        rows = await overlay.ApplyAsync(
            Array.Empty<AppointmentListItemDto>(),
            null,
            date,
            date,
            null,
            null);

        Assert.Equal("Guncel", Assert.Single(rows).PersonFirstName);

        await Task.Delay(10);
        await outbox.EnqueueAsync(OutboxOperation.AppointmentDelete, "temp-1", null);

        rows = await overlay.ApplyAsync(
            Array.Empty<AppointmentListItemDto>(),
            null,
            date,
            date,
            null,
            null);

        Assert.Empty(rows);
        Assert.Null(await overlay.ApplyDetailAsync(null, "temp-1"));
    }

    [Fact]
    public async Task FlushAsync_CreateThenUpdate_RemapsTempIdToServerId()
    {
        string? putPath = null;

        var service = CreateService((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(Json(HttpStatusCode.OK, new
                {
                    success = true,
                    appointmentId = "server-5",
                    error = (string?)null,
                    conflicts = (object?)null,
                }));
            }

            putPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(Json(HttpStatusCode.OK, new
            {
                success = true,
                appointmentId = "server-5",
                error = (string?)null,
                conflicts = (object?)null,
            }));
        });

        await service.EnqueueAsync(OutboxOperation.AppointmentCreate, "temp-9", Input("temp-9"));
        await Task.Delay(10);
        await service.EnqueueAsync(
            OutboxOperation.AppointmentUpdate,
            "temp-9",
            Input("temp-9") with { FirstName = "Guncel" });

        var result = await service.FlushAsync();

        Assert.Equal(2, result.Sent);
        Assert.Equal(0, await service.GetPendingCountAsync());
        Assert.Equal("/v1/appointments/server-5", putPath);
    }

    [Fact]
    public async Task FlushAsync_SeriesCreate_SendsPost()
    {
        string? path = null;
        var service = CreateService((request, _) =>
        {
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(Json(HttpStatusCode.OK, new
            {
                success = true,
                error = (string?)null,
                seriesId = "series-1",
                createdCount = 4,
                skippedCount = 0,
                slots = Array.Empty<object>(),
            }));
        });

        var input = Input("series-req");
        await service.EnqueueAsync(
            OutboxOperation.SeriesCreate,
            "series-req",
            new SeriesOutboxPayload(input, SeriesFrequency.Weekly, 4));

        var result = await service.FlushAsync();

        Assert.Equal(1, result.Sent);
        Assert.Equal(0, await service.GetPendingCountAsync());
        Assert.Equal("/v1/appointments/series", path);
    }

    [Fact]
    public async Task FlushAsync_BulkMove_SendsPost()
    {
        string? path = null;
        var service = CreateService((request, _) =>
        {
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(Json(HttpStatusCode.OK, new
            {
                moved = 2,
                conflictingIds = Array.Empty<string>(),
                conflicts = Array.Empty<object>(),
                failedIds = Array.Empty<string>(),
            }));
        });

        await service.EnqueueAsync(
            OutboxOperation.AppointmentsBulkMove,
            "first",
            new BulkMoveOutboxPayload(
                new[] { "first", "second" },
                DateTime.Today.AddDays(5),
                TimeSpan.FromHours(10),
                IgnoreConflicts: true));

        var result = await service.FlushAsync();

        Assert.Equal(1, result.Sent);
        Assert.Equal("/v1/appointments/bulk-move", path);
    }

    [Fact]
    public async Task FlushAsync_SeriesAppend_SendsPost()
    {
        string? path = null;
        var service = CreateService((request, _) =>
        {
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(Json(HttpStatusCode.OK, new
            {
                success = true,
                error = (string?)null,
                createdCount = 2,
                skippedCount = 0,
                newSeriesCount = 6,
                slots = Array.Empty<object>(),
            }));
        });

        await service.EnqueueAsync(
            OutboxOperation.SeriesAppend,
            "series-1",
            new SeriesAppendOutboxPayload(2));

        var result = await service.FlushAsync();

        Assert.Equal(1, result.Sent);
        Assert.Equal("/v1/appointments/series/series-1/append", path);
    }

    private (OutboxService Outbox, SyncedAppointmentService Sync, PendingAppointmentOverlay Overlay)
        CreateServices(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _settings.SetManyAsync(new Dictionary<string, string>
        {
            [ServerReadService.ReadModeSettingKey] = "1",
        }).GetAwaiter().GetResult();

        var store = new ApiSessionStore(_settings, new FakeProtector());
        store.SaveAsync(new ApiSession(
            "http://localhost:5199",
            "access",
            "refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            new ApiUser("u1", "admin", "Yönetici", "Admin"))).GetAwaiter().GetResult();

        var client = new CgpApiClient(new HttpClient(new StubHandler(handler)));
        var connection = new ApiConnectionService(client, store);
        var reads = new ServerReadService(client, connection, _cache, _settings);
        var outbox = new OutboxService(
            new SqliteDbContextFactory(_databasePath),
            connection,
            client);

        // Okuma/yazma testlerinde yerel servis kullanılmaz.
        var overlay = new PendingAppointmentOverlay(outbox, reads);
        var sync = new SyncedAppointmentService(null!, reads, connection, client, outbox, overlay);
        return (outbox, sync, overlay);
    }

    private OutboxService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => CreateServices(handler).Outbox;

    private static AppointmentInput Input(string requestId) => new(
        "Kuyruk",
        "Test",
        "+905551112233",
        DateTime.Today.AddDays(3).AddHours(9),
        30,
        "host-1",
        "cat-1",
        AppointmentMethod.Phone,
        null,
        null,
        null,
        new[] { 30 },
        IgnoreConflicts: false,
        ClientRequestId: requestId);

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
        => new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, ApiJson.Options),
                Encoding.UTF8,
                "application/json"),
        };

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

    private sealed class InMemoryCache : ISyncCacheStore
    {
        private readonly Dictionary<string, SyncCacheValue> _values = new();

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
}
