using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// P2/P3 okuma-yazma yolu: sunucu oturumu ve okuma modu açıkken okumalar API'den,
/// yazmalar önce API'ye gider; bağlantı yoksa yazma isteği outbox'a alınır.
/// </summary>
public sealed class SyncedAppointmentService : IAppointmentService
{
    private readonly IAppointmentService _local;
    private readonly ServerReadService _reads;
    private readonly ApiConnectionService _connection;
    private readonly CgpApiClient _client;
    private readonly OutboxService _outbox;
    private readonly PendingAppointmentOverlay? _overlay;

    public SyncedAppointmentService(
        IAppointmentService local,
        ServerReadService reads,
        ApiConnectionService connection,
        CgpApiClient client,
        OutboxService outbox,
        PendingAppointmentOverlay? overlay = null)
    {
        _local = local;
        _reads = reads;
        _connection = connection;
        _client = client;
        _outbox = outbox;
        _overlay = overlay;
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> GetDayAsync(DateTime localDate)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetDayAsync(localDate);

        var result = await _reads.GetDayAsync(localDate);
        if (result.Success && result.Data is not null)
        {
            return _overlay is null
                ? result.Data
                : await _overlay.ApplyAsync(
                    result.Data,
                    searchText: null,
                    fromLocal: localDate,
                    toLocal: localDate,
                    hostUserId: null,
                    status: null);
        }

        return await _local.GetDayAsync(localDate);
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(
        string? searchText,
        string? categoryId,
        AppointmentStatus? status,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId = null,
        string? roomId = null)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.SearchAsync(searchText, categoryId, status, fromLocal, toLocal, hostUserId, roomId);

        var result = await _reads.SearchAsync(
            searchText,
            categoryId,
            status?.ToString(),
            fromLocal,
            toLocal,
            hostUserId,
            roomId);

        if (result.Success && result.Data is not null)
        {
            return _overlay is null
                ? result.Data
                : await _overlay.ApplyAsync(
                    result.Data,
                    searchText,
                    fromLocal,
                    toLocal,
                    hostUserId,
                    status);
        }

        return await _local.SearchAsync(searchText, categoryId, status, fromLocal, toLocal, hostUserId, roomId);
    }

    public async Task<AppointmentDetailDto?> GetDetailAsync(string id)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetDetailAsync(id);

        var result = await _reads.GetDetailAsync(id);
        if (result.Success)
        {
            return _overlay is null
                ? result.Data
                : await _overlay.ApplyDetailAsync(result.Data, id);
        }

        // Sunucu kaydı bulamadıysa yerelde eski kopyayı gösterme.
        return result.StatusCode == 404 ? null : await _local.GetDetailAsync(id);
    }

    public async Task<AppointmentListItemDto?> GetNextAppointmentAsync()
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetNextAppointmentAsync();

        var result = await _reads.GetNextAsync();
        if (!result.Success)
            return await _local.GetNextAppointmentAsync();

        if (_overlay is null)
            return result.Data;

        var candidates = await _overlay.ApplyAsync(
            result.Data is null ? Array.Empty<AppointmentListItemDto>() : new[] { result.Data },
            searchText: null,
            fromLocal: null,
            toLocal: null,
            hostUserId: null,
            status: null);

        return candidates.FirstOrDefault(x =>
            x.StartsAtLocal >= DateTime.Now
            && !x.IsDeleted
            && x.Status is AppointmentStatus.Scheduled or AppointmentStatus.Arrived);
    }

    public async Task<IReadOnlyList<DeletedAppointmentDto>> GetDeletedAsync()
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetDeletedAsync();

        var result = await _reads.GetDeletedAsync();
        return result.Success && result.Data is not null
            ? result.Data
            : await _local.GetDeletedAsync();
    }

    // P3'e kadar yazma işlemleri yerel veritabanında yürür.
    public async Task<AppointmentSaveResult> CreateAsync(AppointmentInput input)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.CreateAsync(input);

        var requestId = string.IsNullOrWhiteSpace(input.ClientRequestId)
            ? Guid.NewGuid().ToString("N")
            : input.ClientRequestId;

        var serverInput = input with { ClientRequestId = requestId };
        var session = await _connection.GetAuthorizedSessionAsync();

        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentCreate, requestId, serverInput);
            return AppointmentSaveResult.QueuedOk(requestId);
        }

        var result = await _client.CreateAppointmentAsync(
            session.BaseUrl,
            session.AccessToken,
            serverInput);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentCreate, requestId, serverInput);
            return AppointmentSaveResult.QueuedOk(requestId);
        }

        return AppointmentSaveResult.Fail(result.Error ?? "Görüşme sunucuya kaydedilemedi.");
    }

    public Task<SeriesPlanDto> PreviewSeriesAsync(AppointmentInput input, SeriesRequest request)
        => _local.PreviewSeriesAsync(input, request);

    public async Task<SeriesCreateResult> CreateSeriesAsync(AppointmentInput input, SeriesRequest request)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.CreateSeriesAsync(input, request);

        var requestId = string.IsNullOrWhiteSpace(input.ClientRequestId)
            ? Guid.NewGuid().ToString("N")
            : input.ClientRequestId;

        var serverInput = input with { ClientRequestId = requestId };
        var session = await _connection.GetAuthorizedSessionAsync();

        if (session is null)
        {
            await _outbox.EnqueueAsync(
                OutboxOperation.SeriesCreate,
                requestId,
                new SeriesOutboxPayload(serverInput, request.Frequency, request.Count));
            return SeriesCreateResult.QueuedOk(requestId, request.Count, Array.Empty<SeriesSlotDto>());
        }

        var result = await _client.CreateSeriesAsync(
            session.BaseUrl,
            session.AccessToken,
            serverInput,
            request.Frequency,
            request.Count);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(
                OutboxOperation.SeriesCreate,
                requestId,
                new SeriesOutboxPayload(serverInput, request.Frequency, request.Count));
            return SeriesCreateResult.QueuedOk(requestId, request.Count, Array.Empty<SeriesSlotDto>());
        }

        return SeriesCreateResult.Fail(result.Error ?? "Seri sunucuda oluşturulamadı.");
    }

    public async Task<SeriesCancelResult> CancelSeriesAsync(string seriesId)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.CancelSeriesAsync(seriesId);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.SeriesCancel, seriesId, null);
            return SeriesCancelResult.QueuedOk();
        }

        var result = await _client.CancelSeriesAsync(
            session.BaseUrl,
            session.AccessToken,
            seriesId);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.SeriesCancel, seriesId, null);
            return SeriesCancelResult.QueuedOk();
        }

        return SeriesCancelResult.Fail(result.Error ?? "Seri sunucuda iptal edilemedi.");
    }

    public Task<SeriesAppendPlanDto> PreviewAppendAsync(string seriesId, int count)
        => _local.PreviewAppendAsync(seriesId, count);

    public async Task<SeriesAppendResult> AppendSeriesAsync(string seriesId, int count)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.AppendSeriesAsync(seriesId, count);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(
                OutboxOperation.SeriesAppend,
                seriesId,
                new SeriesAppendOutboxPayload(count));
            return SeriesAppendResult.QueuedOk(count);
        }

        var result = await _client.AppendSeriesAsync(
            session.BaseUrl,
            session.AccessToken,
            seriesId,
            count);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(
                OutboxOperation.SeriesAppend,
                seriesId,
                new SeriesAppendOutboxPayload(count));
            return SeriesAppendResult.QueuedOk(count);
        }

        return SeriesAppendResult.Fail(result.Error ?? "Seriye kayıt eklenemedi.");
    }

    public async Task<SeriesUpdateResult> UpdateSeriesAsync(string seriesId, SeriesUpdateInput input)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.UpdateSeriesAsync(seriesId, input);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.SeriesUpdate, seriesId, input);
            return SeriesUpdateResult.QueuedOk();
        }

        var result = await _client.UpdateSeriesAsync(
            session.BaseUrl,
            session.AccessToken,
            seriesId,
            input);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.SeriesUpdate, seriesId, input);
            return SeriesUpdateResult.QueuedOk();
        }

        return SeriesUpdateResult.Fail(result.Error ?? "Seri güncellenemedi.");
    }

    public async Task<AppointmentSaveResult> UpdateAsync(string id, AppointmentInput input)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.UpdateAsync(id, input);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentUpdate, id, input);
            return AppointmentSaveResult.QueuedOk(id);
        }

        var result = await _client.UpdateAppointmentAsync(
            session.BaseUrl,
            session.AccessToken,
            id,
            input);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentUpdate, id, input);
            return AppointmentSaveResult.QueuedOk(id);
        }

        return AppointmentSaveResult.Fail(result.Error ?? "Görüşme sunucuda güncellenemedi.");
    }

    public async Task<OperationResult> DeleteAsync(string id)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.DeleteAsync(id);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentDelete, id, null);
            return OperationResult.QueuedOk();
        }

        var result = await _client.DeleteAppointmentAsync(
            session.BaseUrl,
            session.AccessToken,
            id);

        if (result.Success)
            return OperationResult.Ok();

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentDelete, id, null);
            return OperationResult.QueuedOk();
        }

        return OperationResult.Fail(result.Error ?? "Görüşme sunucudan silinemedi.");
    }

    public async Task<BulkDeleteResult> DeleteManyAsync(IReadOnlyList<string> ids)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.DeleteManyAsync(ids);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            foreach (var id in ids)
                await _outbox.EnqueueAsync(OutboxOperation.AppointmentDelete, id, null);

            return new BulkDeleteResult(0, Array.Empty<string>())
            {
                Queued = ids.Count,
            };
        }

        var deleted = 0;
        var failed = new List<string>();
        var queued = 0;

        for (var index = 0; index < ids.Count; index++)
        {
            var id = ids[index];
            var result = await _client.DeleteAppointmentAsync(
                session.BaseUrl,
                session.AccessToken,
                id);

            if (result.Success)
            {
                deleted++;
                continue;
            }

            if (result.IsNetworkError)
            {
                for (var remaining = index; remaining < ids.Count; remaining++)
                {
                    await _outbox.EnqueueAsync(OutboxOperation.AppointmentDelete, ids[remaining], null);
                    queued++;
                }

                break;
            }

            failed.Add(id);
        }

        return new BulkDeleteResult(deleted, failed) { Queued = queued };
    }

    public async Task<BulkMoveResult> MoveManyAsync(
        IReadOnlyList<string> ids,
        DateTime newDate,
        TimeSpan? newTime,
        bool ignoreConflicts = false)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.MoveManyAsync(ids, newDate, newTime, ignoreConflicts);

        var payload = new BulkMoveOutboxPayload(ids, newDate, newTime, ignoreConflicts);
        var session = await _connection.GetAuthorizedSessionAsync();

        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentsBulkMove, ids.FirstOrDefault() ?? string.Empty, payload);
            return new BulkMoveResult(0, Array.Empty<string>(), Array.Empty<AppointmentConflictDto>(), Array.Empty<string>())
            {
                Queued = ids.Count,
            };
        }

        var result = await _client.MoveAppointmentsAsync(
            session.BaseUrl,
            session.AccessToken,
            ids,
            newDate,
            newTime,
            ignoreConflicts);

        if (result.Success && result.Data is not null)
            return result.Data;

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentsBulkMove, ids.FirstOrDefault() ?? string.Empty, payload);
            return new BulkMoveResult(0, Array.Empty<string>(), Array.Empty<AppointmentConflictDto>(), Array.Empty<string>())
            {
                Queued = ids.Count,
            };
        }

        return new BulkMoveResult(0, Array.Empty<string>(), Array.Empty<AppointmentConflictDto>(), ids)
        {
            Error = result.Error ?? "Toplu taşıma sunucuda yapılamadı.",
        };
    }

    public async Task<OperationResult> RestoreAsync(string id)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.RestoreAsync(id);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentRestore, id, null);
            return OperationResult.QueuedOk();
        }

        var result = await _client.RestoreAppointmentAsync(
            session.BaseUrl,
            session.AccessToken,
            id);

        if (result.Success)
            return OperationResult.Ok();

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentRestore, id, null);
            return OperationResult.QueuedOk();
        }

        return OperationResult.Fail(result.Error ?? "Görüşme sunucuda geri alınamadı.");
    }

    public Task<int> PurgeExpiredDeletedAsync(DateTime nowUtc)
        => _local.PurgeExpiredDeletedAsync(nowUtc);

    public Task<int> AnonymizeOldAppointmentsAsync(int retentionYears = 2)
        => _local.AnonymizeOldAppointmentsAsync(retentionYears);
}
