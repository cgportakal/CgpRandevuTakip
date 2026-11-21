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

    public SyncedAppointmentService(
        IAppointmentService local,
        ServerReadService reads,
        ApiConnectionService connection,
        CgpApiClient client,
        OutboxService outbox)
    {
        _local = local;
        _reads = reads;
        _connection = connection;
        _client = client;
        _outbox = outbox;
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> GetDayAsync(DateTime localDate)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetDayAsync(localDate);

        var result = await _reads.GetDayAsync(localDate);
        return result.Success && result.Data is not null
            ? result.Data
            : await _local.GetDayAsync(localDate);
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(
        string? searchText,
        string? categoryId,
        AppointmentStatus? status,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId = null)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.SearchAsync(searchText, categoryId, status, fromLocal, toLocal, hostUserId);

        var result = await _reads.SearchAsync(
            searchText,
            categoryId,
            status?.ToString(),
            fromLocal,
            toLocal,
            hostUserId);

        return result.Success && result.Data is not null
            ? result.Data
            : await _local.SearchAsync(searchText, categoryId, status, fromLocal, toLocal, hostUserId);
    }

    public async Task<AppointmentDetailDto?> GetDetailAsync(string id)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetDetailAsync(id);

        var result = await _reads.GetDetailAsync(id);
        if (result.Success)
            return result.Data;

        // Sunucu kaydı bulamadıysa yerelde eski kopyayı gösterme.
        return result.StatusCode == 404 ? null : await _local.GetDetailAsync(id);
    }

    public async Task<AppointmentListItemDto?> GetNextAppointmentAsync()
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetNextAppointmentAsync();

        var result = await _reads.GetNextAsync();
        return result.Success ? result.Data : await _local.GetNextAppointmentAsync();
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
        => await _reads.IsServerReadActiveAsync()
            ? SeriesCreateResult.Fail("Sunucu okuma modunda seri oluşturma P3b'de etkinleşecek.")
            : await _local.CreateSeriesAsync(input, request);

    public async Task<SeriesCancelResult> CancelSeriesAsync(string seriesId)
        => await _reads.IsServerReadActiveAsync()
            ? SeriesCancelResult.Fail("Sunucu okuma modunda seri iptali P3b'de etkinleşecek.")
            : await _local.CancelSeriesAsync(seriesId);

    public Task<SeriesAppendPlanDto> PreviewAppendAsync(string seriesId, int count)
        => _local.PreviewAppendAsync(seriesId, count);

    public async Task<SeriesAppendResult> AppendSeriesAsync(string seriesId, int count)
        => await _reads.IsServerReadActiveAsync()
            ? SeriesAppendResult.Fail("Sunucu okuma modunda seri ekleme P3b'de etkinleşecek.")
            : await _local.AppendSeriesAsync(seriesId, count);

    public async Task<SeriesUpdateResult> UpdateSeriesAsync(string seriesId, SeriesUpdateInput input)
        => await _reads.IsServerReadActiveAsync()
            ? SeriesUpdateResult.Fail("Sunucu okuma modunda seri düzenleme P3b'de etkinleşecek.")
            : await _local.UpdateSeriesAsync(seriesId, input);

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
        => await _reads.IsServerReadActiveAsync()
            ? new BulkDeleteResult(0, ids, "Sunucu okuma modunda toplu silme P3b'de etkinleşecek.")
            : await _local.DeleteManyAsync(ids);

    public async Task<BulkMoveResult> MoveManyAsync(
        IReadOnlyList<string> ids,
        DateTime newDate,
        TimeSpan? newTime,
        bool ignoreConflicts = false)
        => await _reads.IsServerReadActiveAsync()
            ? new BulkMoveResult(
                0,
                Array.Empty<string>(),
                Array.Empty<AppointmentConflictDto>(),
                ids,
                "Sunucu okuma modunda toplu taşıma P3b'de etkinleşecek.")
            : await _local.MoveManyAsync(ids, newDate, newTime, ignoreConflicts);

    public async Task<OperationResult> RestoreAsync(string id)
        => await _reads.IsServerReadActiveAsync()
            ? OperationResult.Fail("Sunucu okuma modunda geri alma P3b'de etkinleşecek.")
            : await _local.RestoreAsync(id);

    public Task<int> PurgeExpiredDeletedAsync(DateTime nowUtc)
        => _local.PurgeExpiredDeletedAsync(nowUtc);

    public Task<int> AnonymizeOldAppointmentsAsync(int retentionYears = 2)
        => _local.AnonymizeOldAppointmentsAsync(retentionYears);
}
