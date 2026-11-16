using System.Globalization;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.AuditLogs;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Reports;

/// <summary>
/// Rapor özetini ve ek rapor türlerini üretir. Tüm sorgular yerel gün sınırlarından UTC'ye
/// çevrilir; host rolü yalnızca kendi görüşmelerini görür.
/// </summary>
public sealed class ReportService : IReportService
{
    private const int DailyBucketLimit = 31;
    private const int MaxPersonResults = 50;
    private const int MaxAuditRecords = 2000;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IReportQueries _queries;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public ReportService(
        IReportQueries queries,
        IAuditLogRepository auditLogs,
        IClock clock,
        ICurrentUser currentUser)
    {
        _queries = queries;
        _auditLogs = auditLogs;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(ReportQuery query)
    {
        var loaded = await LoadAsync(query);
        return BuildSummary(loaded);
    }

    public async Task<ReportResultDto> GetReportAsync(
        ReportKind kind,
        ReportQuery query,
        string? personSearch = null)
    {
        var loaded = await LoadAsync(query);
        var summary = BuildSummary(loaded);

        IReadOnlyList<HostPerformanceDto> hosts = Array.Empty<HostPerformanceDto>();
        IReadOnlyList<CategoryTrendDto> trends = Array.Empty<CategoryTrendDto>();
        IReadOnlyList<AttendanceDayDto> attendance = Array.Empty<AttendanceDayDto>();
        IReadOnlyList<RepeatPersonDto> repeats = Array.Empty<RepeatPersonDto>();
        IReadOnlyList<PersonHistoryPersonDto> history = Array.Empty<PersonHistoryPersonDto>();
        AuditActivityReportDto? audit = null;

        switch (kind)
        {
            case ReportKind.HostPerformance:
                hosts = BuildHostPerformance(loaded.Rows);
                break;

            case ReportKind.CategoryTrend:
                trends = BuildCategoryTrends(loaded.Rows, loaded.Query, loaded.Granularity);
                break;

            case ReportKind.Attendance:
                attendance = BuildAttendance(loaded);
                break;

            case ReportKind.Repeat:
                repeats = BuildRepeats(loaded);
                break;

            case ReportKind.PersonHistory:
                history = BuildPersonHistory(loaded, personSearch);
                break;

            case ReportKind.AuditActivity:
                audit = await BuildAuditActivityAsync(loaded.Query);
                break;
        }

        return new ReportResultDto(
            kind,
            summary,
            hosts,
            trends,
            attendance,
            repeats,
            history,
            audit);
    }

    private async Task<LoadedReport> LoadAsync(ReportQuery query)
    {
        var from = query.FromLocalDate.Date;
        var to = query.ToLocalDate.Date;
        if (to < from)
            (from, to) = (to, from);

        var normalized = new ReportQuery(from, to, query.CategoryId, query.HostUserId);

        var scopeHost = _currentUser.IsInRole(UserRole.Host)
            ? _currentUser.UserId
            : NormalizeOptional(query.HostUserId);

        var fromUtc = _clock.ToUtc(from);
        var toUtc = _clock.ToUtc(to.AddDays(1));

        var rows = await _queries.GetRowsAsync(
            fromUtc,
            toUtc,
            NormalizeOptional(query.CategoryId),
            scopeHost);

        var days = (to - from).Days + 1;
        var granularity = days <= DailyBucketLimit
            ? ReportGranularity.Daily
            : ReportGranularity.Monthly;

        return new LoadedReport(normalized, rows, granularity);
    }

    private ReportSummaryDto BuildSummary(LoadedReport loaded)
    {
        var rows = loaded.Rows;
        var query = loaded.Query;

        if (rows.Count == 0)
            return ReportSummaryDto.Empty(query);

        var localRows = rows
            .Select(x => new LocalRow(x, _clock.ToLocal(x.StartsAtUtc)))
            .ToList();

        var completed = rows.Count(x => x.Status == AppointmentStatus.Completed);
        var arrived = rows.Count(x => x.Status == AppointmentStatus.Arrived);
        var noShow = rows.Count(x => x.Status == AppointmentStatus.NoShow);
        var cancelled = rows.Count(x => x.Status == AppointmentStatus.Cancelled);
        var postponed = rows.Count(x => x.Status == AppointmentStatus.Postponed);
        var scheduled = rows.Count(x => x.Status == AppointmentStatus.Scheduled);

        var personCounts = rows
            .GroupBy(x => x.PersonId)
            .Select(g => g.Count())
            .ToList();

        var totalMinutes = rows.Sum(x => x.DurationMinutes);

        return new ReportSummaryDto(
            query.FromLocalDate,
            query.ToLocalDate,
            rows.Count,
            completed,
            arrived,
            noShow,
            cancelled,
            postponed,
            scheduled,
            personCounts.Count,
            personCounts.Count(x => x > 1),
            rows.Count(x => x.WillRepeat),
            rows.Average(x => x.DurationMinutes),
            totalMinutes,
            loaded.Granularity,
            BuildPoints(localRows, query.FromLocalDate, query.ToLocalDate, loaded.Granularity),
            BuildCategories(rows),
            BuildHosts(rows),
            BuildHours(localRows));
    }

    private static IReadOnlyList<HostPerformanceDto> BuildHostPerformance(
        IReadOnlyList<ReportRow> rows)
        => rows
            .GroupBy(x => new { x.HostUserId, x.HostName })
            .Select(g =>
            {
                var total = g.Count();
                var completed = g.Count(x => x.Status == AppointmentStatus.Completed);
                var arrived = g.Count(x => x.Status == AppointmentStatus.Arrived);
                var noShow = g.Count(x => x.Status == AppointmentStatus.NoShow);
                var cancelled = g.Count(x => x.Status == AppointmentStatus.Cancelled);

                return new HostPerformanceDto(
                    g.Key.HostUserId,
                    g.Key.HostName,
                    total,
                    completed,
                    arrived,
                    noShow,
                    cancelled,
                    total == 0 ? 0 : completed * 100d / total,
                    total == 0 ? 0 : noShow * 100d / total,
                    g.Average(x => x.DurationMinutes));
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.HostName, StringComparer.CurrentCulture)
            .ToList();

    private IReadOnlyList<CategoryTrendDto> BuildCategoryTrends(
        IReadOnlyList<ReportRow> rows,
        ReportQuery query,
        ReportGranularity granularity)
        => rows
            .GroupBy(x => new { x.CategoryId, x.CategoryName, x.CategoryColorHex })
            .Select(g =>
            {
                var local = g
                    .Select(x => new LocalRow(x, _clock.ToLocal(x.StartsAtUtc)))
                    .ToList();

                return new CategoryTrendDto(
                    g.Key.CategoryId,
                    g.Key.CategoryName,
                    g.Key.CategoryColorHex,
                    g.Count(),
                    BuildPoints(local, query.FromLocalDate, query.ToLocalDate, granularity));
            })
            .OrderByDescending(x => x.Total)
            .Take(8)
            .ToList();

    private IReadOnlyList<AttendanceDayDto> BuildAttendance(LoadedReport loaded)
    {
        var byDay = loaded.Rows
            .GroupBy(x => _clock.ToLocal(x.StartsAtUtc).Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<AttendanceDayDto>();
        for (var date = loaded.Query.FromLocalDate; date <= loaded.Query.ToLocalDate; date = date.AddDays(1))
        {
            var bucket = byDay.TryGetValue(date, out var list) ? list : new List<ReportRow>();

            days.Add(new AttendanceDayDto(
                date,
                bucket.Count(x => x.Status == AppointmentStatus.Arrived),
                bucket.Count(x => x.Status == AppointmentStatus.Completed),
                bucket.Count(x => x.Status == AppointmentStatus.NoShow),
                bucket.Count(x => x.Status == AppointmentStatus.Cancelled),
                bucket.Count(x => x.Status == AppointmentStatus.Postponed),
                bucket.Count(x => x.Status == AppointmentStatus.Scheduled)));
        }

        return days;
    }

    private IReadOnlyList<RepeatPersonDto> BuildRepeats(LoadedReport loaded)
    {
        var reference = loaded.Query.ToLocalDate;

        return loaded.Rows
            .GroupBy(x => new { x.PersonId, x.PersonFullName, x.PhoneE164 })
            .Select(g =>
            {
                var last = g.Max(x => _clock.ToLocal(x.StartsAtUtc));
                var days = (reference.Date - last.Date).Days;

                return new RepeatPersonDto(
                    g.Key.PersonId,
                    string.IsNullOrWhiteSpace(g.Key.PersonFullName) ? "—" : g.Key.PersonFullName,
                    g.Key.PhoneE164,
                    g.Count(),
                    last,
                    Math.Max(0, days));
            })
            .Where(x => x.VisitCount > 1 || loaded.Rows.Any(r => r.PersonId == x.PersonId && r.WillRepeat))
            .OrderByDescending(x => x.VisitCount)
            .ThenBy(x => x.PersonName, StringComparer.CurrentCulture)
            .Take(MaxPersonResults)
            .ToList();
    }

    private IReadOnlyList<PersonHistoryPersonDto> BuildPersonHistory(
        LoadedReport loaded,
        string? search)
    {
        var term = NormalizeOptional(search);

        var filtered = term is null
            ? loaded.Rows
            : loaded.Rows
                .Where(x => x.PersonFullName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                            || x.PhoneE164.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return filtered
            .GroupBy(x => new { x.PersonId, x.PersonFullName, x.PhoneE164 })
            .Select(g => new PersonHistoryPersonDto(
                g.Key.PersonId,
                string.IsNullOrWhiteSpace(g.Key.PersonFullName) ? "—" : g.Key.PersonFullName,
                g.Key.PhoneE164,
                g.Count(),
                _clock.ToLocal(g.Max(x => x.StartsAtUtc)),
                g.OrderByDescending(x => x.StartsAtUtc)
                    .Select(x => new PersonHistoryEntryDto(
                        x.AppointmentId,
                        _clock.ToLocal(x.StartsAtUtc),
                        x.HostName,
                        x.CategoryName,
                        x.Status,
                        x.IsClosed,
                        null))
                    .ToList()))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.PersonName, StringComparer.CurrentCulture)
            .Take(MaxPersonResults)
            .ToList();
    }

    private async Task<AuditActivityReportDto> BuildAuditActivityAsync(ReportQuery query)
    {
        var records = await _auditLogs.QueryAsync(new AuditLogQuery(
            UserId: null,
            Action: null,
            FromUtc: _clock.ToUtc(query.FromLocalDate),
            ToUtc: _clock.ToUtc(query.ToLocalDate.AddDays(1)),
            Skip: 0,
            Take: MaxAuditRecords));

        var actions = records
            .GroupBy(x => x.Action)
            .Select(g => new AuditActivityCountDto(AuditActionText.Get(g.Key), g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var entries = records
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => new AuditActivityEntryDto(
                _clock.ToLocal(x.CreatedAtUtc),
                string.IsNullOrWhiteSpace(x.UserName) ? "—" : x.UserName!,
                AuditActionText.Get(x.Action),
                x.EntityType,
                x.EntityId))
            .ToList();

        return new AuditActivityReportDto(records.Count, actions, entries);
    }

    private static IReadOnlyList<ReportPointDto> BuildPoints(
        IReadOnlyList<LocalRow> rows,
        DateTime from,
        DateTime to,
        ReportGranularity granularity)
    {
        var points = new List<ReportPointDto>();

        if (granularity == ReportGranularity.Daily)
        {
            var byDay = rows
                .GroupBy(x => x.LocalDate.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            {
                var bucket = byDay.TryGetValue(date, out var list)
                    ? list
                    : new List<LocalRow>();

                points.Add(new ReportPointDto(
                    date.ToString("dd.MM", Culture),
                    date,
                    bucket.Count,
                    bucket.Count(x => x.Row.Status == AppointmentStatus.Completed),
                    bucket.Count(x => x.Row.Status == AppointmentStatus.NoShow)));
            }

            return points;
        }

        var byMonth = rows
            .GroupBy(x => new DateTime(x.LocalDate.Year, x.LocalDate.Month, 1))
            .ToDictionary(g => g.Key, g => g.ToList());

        var cursor = new DateTime(from.Year, from.Month, 1);
        var last = new DateTime(to.Year, to.Month, 1);

        while (cursor <= last)
        {
            var bucket = byMonth.TryGetValue(cursor, out var list)
                ? list
                : new List<LocalRow>();

            points.Add(new ReportPointDto(
                cursor.ToString("MMM yyyy", Culture),
                cursor,
                bucket.Count,
                bucket.Count(x => x.Row.Status == AppointmentStatus.Completed),
                bucket.Count(x => x.Row.Status == AppointmentStatus.NoShow)));

            cursor = cursor.AddMonths(1);
        }

        return points;
    }

    private static IReadOnlyList<ReportCategoryDto> BuildCategories(IReadOnlyList<ReportRow> rows)
        => rows
            .GroupBy(x => new { x.CategoryId, x.CategoryName, x.CategoryColorHex })
            .Select(g => new ReportCategoryDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Key.CategoryColorHex,
                g.Count(),
                g.Count(x => x.Status == AppointmentStatus.Completed),
                g.Count(x => x.Status == AppointmentStatus.NoShow)))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.CategoryName, StringComparer.CurrentCulture)
            .ToList();

    private static IReadOnlyList<ReportHostDto> BuildHosts(IReadOnlyList<ReportRow> rows)
        => rows
            .GroupBy(x => new { x.HostUserId, x.HostName })
            .Select(g => new ReportHostDto(
                g.Key.HostUserId,
                g.Key.HostName,
                g.Count(),
                g.Count(x => x.Status == AppointmentStatus.Completed),
                g.Count(x => x.Status == AppointmentStatus.NoShow),
                g.Count(x => x.Status == AppointmentStatus.Cancelled)))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.HostName, StringComparer.CurrentCulture)
            .ToList();

    private static IReadOnlyList<ReportHourDto> BuildHours(IReadOnlyList<LocalRow> rows)
        => rows
            .GroupBy(x => x.LocalDate.Hour)
            .Select(g => new ReportHourDto(g.Key, g.Count()))
            .OrderBy(x => x.Hour)
            .ToList();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record LoadedReport(
        ReportQuery Query,
        IReadOnlyList<ReportRow> Rows,
        ReportGranularity Granularity);

    private sealed record LocalRow(ReportRow Row, DateTime LocalDate);
}
