namespace CgpRandevuTakip.Application.Reports;

public interface IReportService
{
    /// <summary>Seçili yerel tarih aralığı için özet rapor üretir.</summary>
    Task<ReportSummaryDto> GetSummaryAsync(ReportQuery query);

    /// <summary>Seçili rapor türünü üretir; kişi geçmişinde arama metni kullanılabilir.</summary>
    Task<ReportResultDto> GetReportAsync(
        ReportKind kind,
        ReportQuery query,
        string? personSearch = null);
}
