using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Domain.Enums;

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
