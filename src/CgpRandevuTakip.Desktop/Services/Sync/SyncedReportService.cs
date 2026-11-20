using CgpRandevuTakip.Application.Reports;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// P2 okuma yolu: raporlar sunucudan alınır; sunucu yoksa veya okuma başarısızsa
/// yerel rapor servisine düşer.
/// </summary>
public sealed class SyncedReportService : IReportService
{
    private readonly IReportService _local;
    private readonly ServerReadService _reads;

    public SyncedReportService(IReportService local, ServerReadService reads)
    {
        _local = local;
        _reads = reads;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(ReportQuery query)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetSummaryAsync(query);

        var result = await _reads.GetReportSummaryAsync(query);
        return result.Success && result.Data is not null
            ? result.Data
            : await _local.GetSummaryAsync(query);
    }

    public async Task<ReportResultDto> GetReportAsync(
        ReportKind kind,
        ReportQuery query,
        string? personSearch = null)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetReportAsync(kind, query, personSearch);

        var result = await _reads.GetReportAsync(kind, query, personSearch);
        return result.Success && result.Data is not null
            ? result.Data
            : await _local.GetReportAsync(kind, query, personSearch);
    }
}
