using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Outbox'taki bekleyen oluşturma/güncelleme/silme/sonuç isteklerini sunucudan
/// gelen listeye uygular. Böylece çevrimdışı oluşturulan kayıtlar bağlantı
/// gelmeden önce de görünür.
/// </summary>
public sealed class PendingAppointmentOverlay
{
    private readonly OutboxService _outbox;
    private readonly ServerReadService _reads;

    public PendingAppointmentOverlay(OutboxService outbox, ServerReadService reads)
    {
        _outbox = outbox;
        _reads = reads;
    }

    public async Task<IReadOnlyList<AppointmentListItemDto>> ApplyAsync(
        IReadOnlyList<AppointmentListItemDto> rows,
        string? searchText,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId,
        AppointmentStatus? status,
        CancellationToken ct = default)
    {
        var items = await _outbox.GetPendingItemsAsync(ct);
        if (items.Count == 0)
            return rows;

        var lookup = await LoadLookupAsync(items, ct);
        var result = rows.ToList();

        foreach (var item in items)
        {
            switch (item.Operation)
            {
                case OutboxOperation.AppointmentCreate:
                {
                    var input = Deserialize<AppointmentInput>(item.Payload);
                    if (input is null)
                        break;

                    if (result.Any(x => x.Id == item.EntityId))
                        break;

                    result.Add(BuildRow(item.EntityId, input, lookup));
                    break;
                }

                case OutboxOperation.AppointmentUpdate:
                {
                    var input = Deserialize<AppointmentInput>(item.Payload);
                    if (input is null)
                        break;

                    var index = result.FindIndex(x => x.Id == item.EntityId);
                    if (index >= 0)
                        result[index] = ApplyInput(result[index], input, lookup);
                    break;
                }

                case OutboxOperation.AppointmentDelete:
                    result.RemoveAll(x => x.Id == item.EntityId);
                    break;

                case OutboxOperation.AppointmentOutcome:
                {
                    var input = Deserialize<AppointmentOutcomeInput>(item.Payload);
                    if (input is null)
                        break;

                    var index = result.FindIndex(x => x.Id == item.EntityId);
                    if (index >= 0)
                    {
                        var row = result[index];
                        result[index] = row with
                        {
                            Status = MapStatus(input.Attendance, input.IsClosed),
                            IsOverdue = false,
                        };
                    }

                    break;
                }

                case OutboxOperation.SeriesCreate:
                {
                    var payload = Deserialize<SeriesOutboxPayload>(item.Payload);
                    if (payload is null)
                        break;

                    var prefix = SeriesPrefix(item.EntityId);
                    if (result.Any(x => x.Id.StartsWith(prefix, StringComparison.Ordinal)))
                        break;

                    foreach (var (date, index) in BuildSeriesDates(payload))
                    {
                        result.Add(BuildRow(
                            prefix + index,
                            payload.Input with { StartsAtLocal = date },
                            lookup));
                    }

                    break;
                }

                case OutboxOperation.SeriesCancel:
                    result.RemoveAll(x => x.Id.StartsWith(
                        SeriesPrefix(item.EntityId),
                        StringComparison.Ordinal));
                    break;

                case OutboxOperation.SeriesAppend:
                {
                    var payload = Deserialize<SeriesAppendOutboxPayload>(item.Payload);
                    var create = items.FirstOrDefault(x =>
                        x.Operation == OutboxOperation.SeriesCreate
                        && x.EntityId == item.EntityId);
                    var series = create is null ? null : Deserialize<SeriesOutboxPayload>(create.Payload);

                    if (payload is null || series is null)
                        break;

                    var prefix = SeriesPrefix(item.EntityId);
                    var existing = result.Count(x => x.Id.StartsWith(prefix, StringComparison.Ordinal));
                    var allDates = SeriesScheduleCalculator.Build(
                        series.Input.StartsAtLocal,
                        series.Frequency,
                        series.Count + payload.Count);

                    for (var index = existing; index < allDates.Count; index++)
                    {
                        result.Add(BuildRow(
                            prefix + (index + 1),
                            series.Input with { StartsAtLocal = allDates[index] },
                            lookup));
                    }

                    break;
                }

                case OutboxOperation.SeriesUpdate:
                {
                    var input = Deserialize<SeriesUpdateInput>(item.Payload);
                    if (input is null)
                        break;

                    var prefix = SeriesPrefix(item.EntityId);
                    for (var index = 0; index < result.Count; index++)
                    {
                        if (result[index].Id.StartsWith(prefix, StringComparison.Ordinal))
                            result[index] = ApplySeriesUpdate(result[index], input, lookup);
                    }

                    break;
                }

                case OutboxOperation.AppointmentsBulkMove:
                {
                    var payload = Deserialize<BulkMoveOutboxPayload>(item.Payload);
                    if (payload is null)
                        break;

                    var ids = payload.Ids.ToHashSet(StringComparer.Ordinal);
                    for (var index = 0; index < result.Count; index++)
                    {
                        var row = result[index];
                        if (!ids.Contains(row.Id))
                            continue;

                        var time = payload.NewTime ?? row.StartsAtLocal.TimeOfDay;
                        var startsAt = payload.NewDate.Date.Add(time);
                        result[index] = row with
                        {
                            StartsAtLocal = startsAt,
                            EndsAtLocal = startsAt.AddMinutes(row.DurationMinutes),
                            IsOverdue = startsAt < DateTime.Now,
                        };
                    }

                    break;
                }
            }
        }

        return result
            .Where(x => Matches(x, searchText, fromLocal, toLocal, hostUserId, status))
            .OrderBy(x => x.StartsAtLocal)
            .ToList();
    }

    public async Task<AppointmentDetailDto?> ApplyDetailAsync(
        AppointmentDetailDto? detail,
        string id,
        CancellationToken ct = default)
    {
        var items = await _outbox.GetPendingItemsAsync(ct);
        if (items.Count == 0)
            return detail;

        var lookup = await LoadLookupAsync(items, ct);

        if (id.StartsWith(SeriesMarker, StringComparison.Ordinal))
            return BuildSeriesDetail(id, items, lookup);

        var current = detail;

        foreach (var item in items.Where(x => x.EntityId == id))
        {
            switch (item.Operation)
            {
                case OutboxOperation.AppointmentCreate:
                {
                    var input = Deserialize<AppointmentInput>(item.Payload);
                    if (input is null)
                        break;

                    current ??= BuildDetail(id, input, lookup);
                    break;
                }

                case OutboxOperation.AppointmentUpdate:
                {
                    var input = Deserialize<AppointmentInput>(item.Payload);
                    if (input is null || current is null)
                        break;

                    current = ApplyDetailInput(current, input, lookup);
                    break;
                }

                case OutboxOperation.AppointmentOutcome:
                {
                    var input = Deserialize<AppointmentOutcomeInput>(item.Payload);
                    if (input is null || current is null)
                        break;

                    var status = MapStatus(input.Attendance, input.IsClosed);
                    var outcome = new AppointmentOutcomeDto(
                        input.Attendance,
                        input.Summary,
                        input.WillRepeat,
                        input.PostNotes,
                        input.IsClosed,
                        input.IsClosed ? DateTime.Now : null,
                        null,
                        DateTime.Now);

                    current = current with
                    {
                        Status = status,
                        IsClosed = input.IsClosed,
                        Outcome = outcome,
                        ClosedAtLocal = input.IsClosed ? DateTime.Now : null,
                    };
                    break;
                }

                case OutboxOperation.AppointmentDelete:
                    current = null;
                    break;

                case OutboxOperation.AppointmentsBulkMove:
                {
                    var payload = Deserialize<BulkMoveOutboxPayload>(item.Payload);
                    if (payload is null || current is null || !payload.Ids.Contains(id, StringComparer.Ordinal))
                        break;

                    var time = payload.NewTime ?? current.StartsAtLocal.TimeOfDay;
                    var startsAt = payload.NewDate.Date.Add(time);
                    current = current with
                    {
                        StartsAtLocal = startsAt,
                        EndsAtLocal = startsAt.AddMinutes(current.DurationMinutes),
                        IsOverdue = startsAt < DateTime.Now,
                    };
                    break;
                }
            }
        }

        return current;
    }

    private async Task<Lookup> LoadLookupAsync(
        IReadOnlyList<OutboxPendingItem> items,
        CancellationToken ct)
    {
        var needsLookup = items.Any(x =>
            x.Operation is OutboxOperation.AppointmentCreate or OutboxOperation.AppointmentUpdate);

        if (!needsLookup)
            return Lookup.Empty;

        var manifest = await _reads.GetManifestAsync(ct: ct);
        if (!manifest.Success || manifest.Data is null)
            return Lookup.Empty;

        return new Lookup(
            manifest.Data.Categories.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal),
            manifest.Data.Hosts.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal));
    }

    private static AppointmentListItemDto BuildRow(
        string id,
        AppointmentInput input,
        Lookup lookup)
    {
        var category = lookup.Categories.GetValueOrDefault(input.CategoryId);
        var host = lookup.Hosts.GetValueOrDefault(input.HostUserId);
        var student = string.Join(
            ' ',
            new[] { input.StudentFirstName, input.StudentLastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new AppointmentListItemDto(
            id,
            input.StartsAtLocal,
            input.StartsAtLocal.AddMinutes(input.DurationMinutes),
            input.DurationMinutes,
            input.FirstName,
            input.LastName,
            input.Phone,
            input.HostUserId,
            host?.DisplayName ?? input.HostUserId,
            input.CategoryId,
            category?.Name ?? input.CategoryId,
            category?.ColorHex ?? "#2E7D5B",
            input.Method,
            AppointmentStatus.Scheduled,
            input.StartsAtLocal < DateTime.Now,
            false,
            string.IsNullOrWhiteSpace(student) ? null : student);
    }

    private static AppointmentListItemDto ApplyInput(
        AppointmentListItemDto row,
        AppointmentInput input,
        Lookup lookup)
    {
        var category = lookup.Categories.GetValueOrDefault(input.CategoryId);
        var host = lookup.Hosts.GetValueOrDefault(input.HostUserId);
        var student = string.Join(
            ' ',
            new[] { input.StudentFirstName, input.StudentLastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return row with
        {
            StartsAtLocal = input.StartsAtLocal,
            EndsAtLocal = input.StartsAtLocal.AddMinutes(input.DurationMinutes),
            DurationMinutes = input.DurationMinutes,
            PersonFirstName = input.FirstName,
            PersonLastName = input.LastName,
            PhoneE164 = input.Phone,
            HostUserId = input.HostUserId,
            HostName = host?.DisplayName ?? input.HostUserId,
            CategoryId = input.CategoryId,
            CategoryName = category?.Name ?? input.CategoryId,
            CategoryColorHex = category?.ColorHex ?? row.CategoryColorHex,
            Method = input.Method,
            StudentFullName = string.IsNullOrWhiteSpace(student) ? null : student,
        };
    }

    private static AppointmentDetailDto BuildDetail(
        string id,
        AppointmentInput input,
        Lookup lookup)
    {
        var category = lookup.Categories.GetValueOrDefault(input.CategoryId);
        var host = lookup.Hosts.GetValueOrDefault(input.HostUserId);

        return new AppointmentDetailDto(
            id,
            input.StartsAtLocal,
            input.StartsAtLocal.AddMinutes(input.DurationMinutes),
            input.DurationMinutes,
            input.FirstName,
            input.LastName,
            input.Phone,
            input.HostUserId,
            host?.DisplayName ?? input.HostUserId,
            input.CategoryId,
            category?.Name ?? input.CategoryId,
            category?.ColorHex ?? "#2E7D5B",
            input.Method,
            AppointmentStatus.Scheduled,
            input.Description,
            input.StudentFirstName,
            input.StudentLastName,
            input.ReminderOffsetsMinutes,
            input.StartsAtLocal < DateTime.Now,
            false,
            false,
            DateTime.Now,
            DateTime.Now,
            null,
            null,
            null);
    }

    private static AppointmentDetailDto ApplyDetailInput(
        AppointmentDetailDto detail,
        AppointmentInput input,
        Lookup lookup)
    {
        var category = lookup.Categories.GetValueOrDefault(input.CategoryId);
        var host = lookup.Hosts.GetValueOrDefault(input.HostUserId);

        return detail with
        {
            StartsAtLocal = input.StartsAtLocal,
            EndsAtLocal = input.StartsAtLocal.AddMinutes(input.DurationMinutes),
            DurationMinutes = input.DurationMinutes,
            PersonFirstName = input.FirstName,
            PersonLastName = input.LastName,
            PhoneE164 = input.Phone,
            HostUserId = input.HostUserId,
            HostName = host?.DisplayName ?? input.HostUserId,
            CategoryId = input.CategoryId,
            CategoryName = category?.Name ?? input.CategoryId,
            CategoryColorHex = category?.ColorHex ?? detail.CategoryColorHex,
            Method = input.Method,
            Description = input.Description,
            StudentFirstName = input.StudentFirstName,
            StudentLastName = input.StudentLastName,
            ReminderOffsetsMinutes = input.ReminderOffsetsMinutes,
            IsOverdue = input.StartsAtLocal < DateTime.Now,
        };
    }

    private static bool Matches(
        AppointmentListItemDto row,
        string? searchText,
        DateTime? fromLocal,
        DateTime? toLocal,
        string? hostUserId,
        AppointmentStatus? status)
    {
        var date = row.StartsAtLocal.Date;

        if (fromLocal is not null && date < fromLocal.Value.Date)
            return false;
        if (toLocal is not null && date > toLocal.Value.Date)
            return false;
        if (!string.IsNullOrWhiteSpace(hostUserId)
            && !string.Equals(row.HostUserId, hostUserId, StringComparison.Ordinal))
        {
            return false;
        }

        if (status is not null && row.Status != status.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            var matches = row.PersonFullName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                          || row.PhoneE164.Contains(term, StringComparison.Ordinal)
                          || (row.StudentFullName?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false);

            if (!matches)
                return false;
        }

        return true;
    }

    private static AppointmentStatus MapStatus(Attendance attendance, bool closed)
        => attendance switch
        {
            Attendance.Arrived => closed ? AppointmentStatus.Completed : AppointmentStatus.Arrived,
            Attendance.NoShow => AppointmentStatus.NoShow,
            Attendance.Postponed => AppointmentStatus.Postponed,
            Attendance.Cancelled => AppointmentStatus.Cancelled,
            _ => AppointmentStatus.Scheduled,
        };

    private const string SeriesMarker = "series:";

    private static string SeriesPrefix(string seriesId) => SeriesMarker + seriesId + ":";

    private static IEnumerable<(DateTime Date, int Index)> BuildSeriesDates(SeriesOutboxPayload payload)
        => SeriesScheduleCalculator
            .Build(payload.Input.StartsAtLocal, payload.Frequency, payload.Count)
            .Select((date, index) => (Date: date, Index: index + 1))
            .Where(x => x.Date > DateTime.Now);

    private static AppointmentListItemDto ApplySeriesUpdate(
        AppointmentListItemDto row,
        SeriesUpdateInput input,
        Lookup lookup)
    {
        var category = lookup.Categories.GetValueOrDefault(input.CategoryId);
        var host = lookup.Hosts.GetValueOrDefault(input.HostUserId);
        var startsAt = input.TimeOfDay is null
            ? row.StartsAtLocal
            : row.StartsAtLocal.Date.Add(input.TimeOfDay.Value);

        return row with
        {
            StartsAtLocal = startsAt,
            EndsAtLocal = startsAt.AddMinutes(input.DurationMinutes),
            DurationMinutes = input.DurationMinutes,
            HostUserId = input.HostUserId,
            HostName = host?.DisplayName ?? input.HostUserId,
            CategoryId = input.CategoryId,
            CategoryName = category?.Name ?? input.CategoryId,
            CategoryColorHex = category?.ColorHex ?? row.CategoryColorHex,
            Method = input.Method,
            StudentFullName = string.Join(
                ' ',
                new[] { input.StudentFirstName, input.StudentLastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } student
                ? student
                : null,
        };
    }

    private static AppointmentDetailDto? BuildSeriesDetail(
        string id,
        IReadOnlyList<OutboxPendingItem> items,
        Lookup lookup)
    {
        var inner = id[SeriesMarker.Length..];
        var separator = inner.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(inner[(separator + 1)..], out var index))
            return null;

        var seriesId = inner[..separator];
        var create = items.FirstOrDefault(x =>
            x.Operation == OutboxOperation.SeriesCreate
            && x.EntityId == seriesId);

        if (create is null)
            return null;

        var payload = Deserialize<SeriesOutboxPayload>(create.Payload);
        if (payload is null || index < 1)
            return null;

        var dates = SeriesScheduleCalculator.Build(
            payload.Input.StartsAtLocal,
            payload.Frequency,
            payload.Count);

        if (index > dates.Count)
            return null;

        var detail = BuildDetail(
            id,
            payload.Input with { StartsAtLocal = dates[index - 1] },
            lookup) with
        {
            SeriesId = seriesId,
            SeriesIndex = index,
            SeriesCount = payload.Count,
        };

        foreach (var updateItem in items.Where(x =>
                     x.Operation == OutboxOperation.SeriesUpdate
                     && x.EntityId == seriesId))
        {
            var input = Deserialize<SeriesUpdateInput>(updateItem.Payload);
            if (input is null)
                continue;

            var category = lookup.Categories.GetValueOrDefault(input.CategoryId);
            var host = lookup.Hosts.GetValueOrDefault(input.HostUserId);
            var startsAt = input.TimeOfDay is null
                ? detail.StartsAtLocal
                : detail.StartsAtLocal.Date.Add(input.TimeOfDay.Value);

            detail = detail with
            {
                StartsAtLocal = startsAt,
                EndsAtLocal = startsAt.AddMinutes(input.DurationMinutes),
                DurationMinutes = input.DurationMinutes,
                HostUserId = input.HostUserId,
                HostName = host?.DisplayName ?? input.HostUserId,
                CategoryId = input.CategoryId,
                CategoryName = category?.Name ?? input.CategoryId,
                CategoryColorHex = category?.ColorHex ?? detail.CategoryColorHex,
                Method = input.Method,
                Description = input.Description,
                StudentFirstName = input.StudentFirstName,
                StudentLastName = input.StudentLastName,
                ReminderOffsetsMinutes = input.ReminderOffsetsMinutes,
            };
        }

        return detail;
    }

    private static T? Deserialize<T>(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return default;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(payload, Api.ApiJson.Options);
        }
        catch (System.Text.Json.JsonException)
        {
            return default;
        }
    }

    private sealed record Lookup(
        IReadOnlyDictionary<string, CategoryDto> Categories,
        IReadOnlyDictionary<string, HostDto> Hosts)
    {
        public static Lookup Empty { get; } = new(
            new Dictionary<string, CategoryDto>(StringComparer.Ordinal),
            new Dictionary<string, HostDto>(StringComparer.Ordinal));
    }
}
