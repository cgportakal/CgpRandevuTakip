namespace CgpRandevuTakip.Application.Reports;

/// <summary>Rapor aralığı yerel gün bazındadır; bitiş günü dahildir.</summary>
public sealed record ReportQuery(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    string? CategoryId = null,
    string? HostUserId = null)
{
    public int DayCount => Math.Max(1, (ToLocalDate.Date - FromLocalDate.Date).Days + 1);
}

/// <summary>Grafik noktası: kısa aralıklarda gün, uzun aralıklarda ay.</summary>
public sealed record ReportPointDto(
    string Label,
    DateTime BucketStartLocal,
    int Total,
    int Completed,
    int NoShow);

public sealed record ReportCategoryDto(
    string CategoryId,
    string CategoryName,
    string ColorHex,
    int Total,
    int Completed,
    int NoShow);

public sealed record ReportHostDto(
    string HostUserId,
    string HostName,
    int Total,
    int Completed,
    int NoShow,
    int Cancelled);

public sealed record ReportHourDto(int Hour, int Total)
{
    public string Label => $"{Hour:00}:00";
}

/// <summary>Grafik kırılımı: 31 güne kadar günlük, daha uzun aralıkta aylık.</summary>
public enum ReportGranularity
{
    Daily = 0,
    Monthly = 1
}

public sealed record ReportSummaryDto(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    int Total,
    int Completed,
    int Arrived,
    int NoShow,
    int Cancelled,
    int Postponed,
    int Scheduled,
    int UniquePersons,
    int ReturningPersons,
    int RepeatRequested,
    double AverageDurationMinutes,
    int TotalMinutes,
    ReportGranularity Granularity,
    IReadOnlyList<ReportPointDto> Points,
    IReadOnlyList<ReportCategoryDto> Categories,
    IReadOnlyList<ReportHostDto> Hosts,
    IReadOnlyList<ReportHourDto> Hours)
{
    public bool IsEmpty => Total == 0;

    public string RangeText => $"{FromLocalDate:dd/MM/yyyy} – {ToLocalDate:dd/MM/yyyy}";

    public double CompletionRate => Rate(Completed);

    public double NoShowRate => Rate(NoShow);

    public double CancelRate => Rate(Cancelled);

    public string CompletionRateText => $"{CompletionRate:0.#}%";

    public string NoShowRateText => $"{NoShowRate:0.#}%";

    public string CancelRateText => $"{CancelRate:0.#}%";

    public string AverageDurationText => $"{AverageDurationMinutes:0} dk";

    public string TotalDurationText
        => TotalMinutes < 60
            ? $"{TotalMinutes} dk"
            : $"{TotalMinutes / 60} sa {TotalMinutes % 60} dk";

    public string BucketLabel => Granularity == ReportGranularity.Monthly
        ? "Aylık dağılım"
        : "Günlük dağılım";

    public static ReportSummaryDto Empty(ReportQuery query)
        => new(
            query.FromLocalDate.Date,
            query.ToLocalDate.Date,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            ReportGranularity.Daily,
            Array.Empty<ReportPointDto>(),
            Array.Empty<ReportCategoryDto>(),
            Array.Empty<ReportHostDto>(),
            Array.Empty<ReportHourDto>());

    private double Rate(int value) => Total == 0 ? 0 : value * 100d / Total;
}

/// <summary>Rapor dışa aktarma biçimi.</summary>
public enum ReportExportFormat
{
    Csv = 0,
    Pdf = 1
}
