using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Reports;

/// <summary>Rapor türü. Her tür aynı tarih aralığı ve filtreleri kullanır.</summary>
public enum ReportKind
{
    Summary = 0,
    HostPerformance = 1,
    CategoryTrend = 2,
    Attendance = 3,
    Repeat = 4,
    PersonHistory = 5,
    AuditActivity = 6
}

public sealed record HostPerformanceDto(
    string HostUserId,
    string HostName,
    int Total,
    int Completed,
    int Arrived,
    int NoShow,
    int Cancelled,
    double CompletionRate,
    double NoShowRate,
    double AverageDurationMinutes,
    int ParticipantTotal = 0)
{
    public string CompletionRateText => $"{CompletionRate:0.#}%";

    public string NoShowRateText => $"{NoShowRate:0.#}%";

    public string AverageDurationText => $"{AverageDurationMinutes:0} dk";
}

public sealed record CategoryTrendDto(
    string CategoryId,
    string CategoryName,
    string ColorHex,
    int Total,
    IReadOnlyList<ReportPointDto> Points);

public sealed record AttendanceDayDto(
    DateTime DateLocal,
    int Arrived,
    int Completed,
    int NoShow,
    int Cancelled,
    int Postponed,
    int Scheduled,
    int GuestArrived = 0,
    int GuestNoShow = 0,
    int GuestCancelled = 0,
    int GuestPostponed = 0)
{
    public string DateText => DateLocal.ToString("dd/MM/yyyy");

    public string WeekdayText => DateLocal.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

    public int Total => Arrived + Completed + NoShow + Cancelled + Postponed + Scheduled;
}

public sealed record RepeatPersonDto(
    string PersonId,
    string PersonName,
    string Phone,
    int VisitCount,
    DateTime LastVisitLocal,
    int DaysSinceLastVisit)
{
    public string LastVisitText => LastVisitLocal.ToString("dd/MM/yyyy");

    public string IntervalText => DaysSinceLastVisit <= 0 ? "—" : $"{DaysSinceLastVisit} gün";
}

public sealed record PersonHistoryEntryDto(
    string AppointmentId,
    DateTime StartsAtLocal,
    string HostName,
    string CategoryName,
    AppointmentStatus Status,
    bool IsClosed,
    string? OutcomeSummary)
{
    public string DateText => StartsAtLocal.ToString("dd/MM/yyyy HH:mm");

    public string StatusText => Status switch
    {
        AppointmentStatus.Scheduled => "Planlandı",
        AppointmentStatus.Arrived => "Geldi",
        AppointmentStatus.Completed => "Tamamlandı",
        AppointmentStatus.NoShow => "Gelmedi",
        AppointmentStatus.Postponed => "Ertelendi",
        AppointmentStatus.Cancelled => "İptal",
        _ => Status.ToString()
    };
}

public sealed record PersonHistoryPersonDto(
    string PersonId,
    string PersonName,
    string Phone,
    int Total,
    DateTime LastVisitLocal,
    IReadOnlyList<PersonHistoryEntryDto> Entries)
{
    public string LastVisitText => LastVisitLocal.ToString("dd/MM/yyyy");

    public string SummaryText => $"{Total} görüşme · son {LastVisitText}";
}

public sealed record AuditActivityCountDto(string ActionName, int Count);

public sealed record AuditActivityEntryDto(
    DateTime CreatedAtLocal,
    string UserName,
    string ActionName,
    string? EntityType,
    string? EntityId)
{
    public string CreatedAtText => CreatedAtLocal.ToString("dd/MM/yyyy HH:mm");

    public string EntityText => string.IsNullOrWhiteSpace(EntityType) ? "—" : EntityType;
}

public sealed record AuditActivityReportDto(
    int Total,
    IReadOnlyList<AuditActivityCountDto> Actions,
    IReadOnlyList<AuditActivityEntryDto> Entries);

/// <summary>Tüm rapor türlerini tek yanıtta taşıyan sonuç.</summary>
public sealed record ReportResultDto(
    ReportKind Kind,
    ReportSummaryDto Summary,
    IReadOnlyList<HostPerformanceDto> HostPerformance,
    IReadOnlyList<CategoryTrendDto> CategoryTrends,
    IReadOnlyList<AttendanceDayDto> Attendance,
    IReadOnlyList<RepeatPersonDto> RepeatPersons,
    IReadOnlyList<PersonHistoryPersonDto> PersonHistory,
    AuditActivityReportDto? AuditActivity)
{
    public static ReportResultDto Empty(ReportKind kind, ReportQuery query)
        => new(
            kind,
            ReportSummaryDto.Empty(query),
            Array.Empty<HostPerformanceDto>(),
            Array.Empty<CategoryTrendDto>(),
            Array.Empty<AttendanceDayDto>(),
            Array.Empty<RepeatPersonDto>(),
            Array.Empty<PersonHistoryPersonDto>(),
            null);

    public bool IsEmpty => Summary.IsEmpty;
}
