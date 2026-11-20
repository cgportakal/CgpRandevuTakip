using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// P2 okuma yolu: sunucu oturumu varsa okumalar API'den (çevrimdışıyken önbellekten),
/// yoksa yerel veritabanından yapılır. Yazmalar P3'e kadar yerelde kalır.
/// </summary>
public sealed class SyncedAppointmentService : IAppointmentService
{
    private readonly IAppointmentService _local;
    private readonly ServerReadService _reads;

    public SyncedAppointmentService(IAppointmentService local, ServerReadService reads)
    {
        _local = local;
        _reads = reads;
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
    public Task<AppointmentSaveResult> CreateAsync(AppointmentInput input)
        => _local.CreateAsync(input);

    public Task<SeriesPlanDto> PreviewSeriesAsync(AppointmentInput input, SeriesRequest request)
        => _local.PreviewSeriesAsync(input, request);

    public Task<SeriesCreateResult> CreateSeriesAsync(AppointmentInput input, SeriesRequest request)
        => _local.CreateSeriesAsync(input, request);

    public Task<SeriesCancelResult> CancelSeriesAsync(string seriesId)
        => _local.CancelSeriesAsync(seriesId);

    public Task<SeriesAppendPlanDto> PreviewAppendAsync(string seriesId, int count)
        => _local.PreviewAppendAsync(seriesId, count);

    public Task<SeriesAppendResult> AppendSeriesAsync(string seriesId, int count)
        => _local.AppendSeriesAsync(seriesId, count);

    public Task<SeriesUpdateResult> UpdateSeriesAsync(string seriesId, SeriesUpdateInput input)
        => _local.UpdateSeriesAsync(seriesId, input);

    public Task<AppointmentSaveResult> UpdateAsync(string id, AppointmentInput input)
        => _local.UpdateAsync(id, input);

    public Task<OperationResult> DeleteAsync(string id)
        => _local.DeleteAsync(id);

    public Task<BulkDeleteResult> DeleteManyAsync(IReadOnlyList<string> ids)
        => _local.DeleteManyAsync(ids);

    public Task<BulkMoveResult> MoveManyAsync(
        IReadOnlyList<string> ids,
        DateTime newDate,
        TimeSpan? newTime,
        bool ignoreConflicts = false)
        => _local.MoveManyAsync(ids, newDate, newTime, ignoreConflicts);

    public Task<OperationResult> RestoreAsync(string id)
        => _local.RestoreAsync(id);

    public Task<int> PurgeExpiredDeletedAsync(DateTime nowUtc)
        => _local.PurgeExpiredDeletedAsync(nowUtc);

    public Task<int> AnonymizeOldAppointmentsAsync(int retentionYears = 2)
        => _local.AnonymizeOldAppointmentsAsync(retentionYears);
}
