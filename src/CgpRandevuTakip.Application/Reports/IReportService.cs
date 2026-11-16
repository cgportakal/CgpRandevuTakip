namespace CgpRandevuTakip.Application.Reports;

public interface IReportService
{
    /// <summary>Seçili yerel tarih aralığı için özet rapor üretir.</summary>
    Task<ReportSummaryDto> GetSummaryAsync(ReportQuery query);
}
