using System.Globalization;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Reports;

/// <summary>
/// Rapor özetini üretir. Tüm sorgular yerel gün sınırlarından UTC'ye çevrilir;
/// host rolü yalnızca kendi görüşmelerini görür.
/// </summary>
public sealed class ReportService : IReportService
{
    private const int DailyBucketLimit = 31;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IReportQueries _queries;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public ReportService(
        IReportQueries queries,
        IClock clock,
        ICurrentUser currentUser)
    {
        _queries = queries;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(ReportQuery query)
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

        if (rows.Count == 0)
            return ReportSummaryDto.Empty(normalized);

        var days = (to - from).Days + 1;
        var granularity = days <= DailyBucketLimit
            ? ReportGranularity.Daily
            : ReportGranularity.Monthly;

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
            from,
            to,
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
            rows.Count == 0 ? 0 : rows.Average(x => x.DurationMinutes),
            totalMinutes,
            granularity,
            BuildPoints(localRows, from, to, granularity),
            BuildCategories(rows),
            BuildHosts(rows),
            BuildHours(localRows));
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

    private sealed record LocalRow(ReportRow Row, DateTime LocalDate);
}
