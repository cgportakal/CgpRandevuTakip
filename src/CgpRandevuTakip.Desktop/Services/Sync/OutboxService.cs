using System.Text.Json;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Desktop.Services.Sync;

public enum OutboxOperation
{
    AppointmentCreate = 0,
    AppointmentUpdate = 1,
    AppointmentDelete = 2,
    AppointmentOutcome = 3,
    AppointmentRestore = 4,
    SeriesCreate = 5,
    SeriesCancel = 6,
    SeriesAppend = 7,
    SeriesUpdate = 8,
    AppointmentsBulkMove = 9
}

public sealed record OutboxFlushResult(
    int Sent,
    int Failed,
    bool Offline,
    string? Error)
{
    public static OutboxFlushResult Empty { get; } = new(0, 0, false, null);
}

public sealed record OutboxPendingItem(
    string Id,
    OutboxOperation Operation,
    string EntityId,
    string Payload,
    DateTime CreatedAtUtc);

public sealed record SeriesOutboxPayload(
    AppointmentInput Input,
    SeriesFrequency Frequency,
    int Count);

public sealed record SeriesAppendOutboxPayload(int Count);

public sealed record BulkMoveOutboxPayload(
    IReadOnlyList<string> Ids,
    DateTime NewDate,
    TimeSpan? NewTime,
    bool IgnoreConflicts);

/// <summary>
/// Sunucuya gönderilemeyen yazma isteklerini yerel kuyrukta tutar ve bağlantı
/// geldiğinde oluşturulma sırasıyla gönderir.
/// </summary>
public sealed class OutboxService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ApiConnectionService _connection;
    private readonly CgpApiClient _client;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public OutboxService(
        IDbContextFactory<AppDbContext> factory,
        ApiConnectionService connection,
        CgpApiClient client)
    {
        _factory = factory;
        _connection = connection;
        _client = client;
    }

    public async Task EnqueueAsync(
        OutboxOperation operation,
        string entityId,
        object? payload,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        if (operation == OutboxOperation.AppointmentCreate)
        {
            var exists = await db.SyncOutboxItems.AnyAsync(
                x => x.Operation == operation.ToString()
                     && x.EntityId == entityId
                     && x.State == "pending",
                ct);

            if (exists)
                return;
        }

        db.SyncOutboxItems.Add(new SyncOutboxItem
        {
            Operation = operation.ToString(),
            EntityId = entityId,
            Payload = payload is null ? string.Empty : JsonSerializer.Serialize(payload, ApiJson.Options),
            State = "pending",
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SyncOutboxItems.CountAsync(x => x.State == "pending", ct);
    }

    public async Task<int> GetFailedCountAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SyncOutboxItems.CountAsync(x => x.State == "failed", ct);
    }

    public async Task<int> GetConflictCountAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SyncOutboxItems.CountAsync(x => x.State == "conflict", ct);
    }

    public async Task<IReadOnlyList<OutboxPendingItem>> GetConflictsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var items = await db.SyncOutboxItems
            .AsNoTracking()
            .Where(x => x.State == "conflict")
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return items
            .Where(x => Enum.TryParse<OutboxOperation>(x.Operation, out _))
            .Select(x => new OutboxPendingItem(
                x.Id,
                Enum.Parse<OutboxOperation>(x.Operation),
                x.EntityId,
                x.Payload,
                x.CreatedAtUtc))
            .ToList();
    }

    /// <summary>
    /// Çakışan kaydı çözer. Yerel seçilirse güncel sunucu sürümüne göre bekleyen
    /// duruma döner; sunucu seçilirse kuyruk kaydı silinir.
    /// </summary>
    public async Task ResolveConflictAsync(
        string id,
        bool useLocal,
        DateTime? serverUpdatedAtUtc,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var item = await db.SyncOutboxItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return;

        if (!useLocal)
        {
            db.SyncOutboxItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return;
        }

        if (item.Operation == OutboxOperation.AppointmentUpdate.ToString()
            && serverUpdatedAtUtc is not null)
        {
            var input = Deserialize<AppointmentInput>(item.Payload);
            if (input is not null)
            {
                item.Payload = JsonSerializer.Serialize(
                    input with { ExpectedUpdatedAtUtc = serverUpdatedAtUtc },
                    ApiJson.Options);
            }
        }

        item.State = "pending";
        item.Attempts = 0;
        item.LastError = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxPendingItem>> GetPendingItemsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var items = await db.SyncOutboxItems
            .AsNoTracking()
            .Where(x => x.State == "pending")
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return items
            .Where(x => Enum.TryParse<OutboxOperation>(x.Operation, out _))
            .Select(x => new OutboxPendingItem(
                x.Id,
                Enum.Parse<OutboxOperation>(x.Operation),
                x.EntityId,
                x.Payload,
                x.CreatedAtUtc))
            .ToList();
    }

    public async Task<int> RetryFailedAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SyncOutboxItems
            .Where(x => x.State == "failed")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.State, "pending")
                    .SetProperty(x => x.LastError, (string?)null),
                ct);
    }

    public async Task<OutboxFlushResult> FlushAsync(CancellationToken ct = default)
    {
        await _flushLock.WaitAsync(ct);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var pending = await db.SyncOutboxItems
                .Where(x => x.State == "pending")
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync(ct);

            if (pending.Count == 0)
                return OutboxFlushResult.Empty;

            var session = await _connection.GetAuthorizedSessionAsync(ct: ct);
            if (session is null)
                return new OutboxFlushResult(0, 0, true, "Sunucu oturumu yok.");

            var sent = 0;
            var failed = 0;

            foreach (var item in pending)
            {
                var result = await ExecuteAsync(session, item, ct);
                if (result.Success)
                {
                    // Oluşturma başarılıysa aynı geçici kimliği kullanan sonraki
                    // güncelleme/silme/sonuç isteklerini sunucu kimliğine bağla.
                    if ((item.Operation == OutboxOperation.AppointmentCreate.ToString()
                         || item.Operation == OutboxOperation.SeriesCreate.ToString())
                        && !string.IsNullOrWhiteSpace(result.ServerId))
                    {
                        foreach (var dependent in pending.Where(x =>
                                     !ReferenceEquals(x, item)
                                     && x.State == "pending"
                                     && x.EntityId == item.EntityId))
                        {
                            dependent.EntityId = result.ServerId!;
                        }

                        // Bekleyen toplu taşıma isteklerindeki geçici kimliği de değiştir.
                        foreach (var dependent in pending.Where(x =>
                                     !ReferenceEquals(x, item)
                                     && x.State == "pending"
                                     && x.Operation == OutboxOperation.AppointmentsBulkMove.ToString()))
                        {
                            var move = Deserialize<BulkMoveOutboxPayload>(dependent.Payload);
                            if (move is null || !move.Ids.Contains(item.EntityId, StringComparer.Ordinal))
                                continue;

                            dependent.Payload = JsonSerializer.Serialize(
                                move with
                                {
                                    Ids = move.Ids
                                        .Select(x => string.Equals(x, item.EntityId, StringComparison.Ordinal)
                                            ? result.ServerId!
                                            : x)
                                        .ToList(),
                                },
                                ApiJson.Options);
                        }

                        // Aynı anda kuyruğa eklenmiş (bu listede olmayan) bağımlı
                        // istekleri de veritabanında sunucu kimliğine bağla.
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE SyncOutboxItems SET EntityId = {result.ServerId} WHERE State = 'pending' AND EntityId = {item.EntityId} AND Id <> {item.Id}",
                            ct);
                    }

                    db.SyncOutboxItems.Remove(item);
                    sent++;
                    continue;
                }

                item.Attempts++;
                item.LastError = Truncate(result.Error);

                if (result.IsNetworkError)
                {
                    await db.SaveChangesAsync(ct);
                    return new OutboxFlushResult(sent, failed, true, result.Error);
                }

                if (result.VersionConflict)
                {
                    // Kullanıcı çözene kadar tekrar denenmez; veri kaybını önlemek için
                    // kayıt kuyrukta "conflict" durumunda tutulur.
                    item.State = "conflict";
                    failed++;
                    continue;
                }

                item.State = "failed";
                failed++;

                // Oluşturma kalıcı olarak reddedildiyse ona bağlı istekleri de düşür.
                if (item.Operation == OutboxOperation.AppointmentCreate.ToString()
                    || item.Operation == OutboxOperation.SeriesCreate.ToString())
                {
                    foreach (var dependent in pending.Where(x =>
                                 !ReferenceEquals(x, item)
                                 && x.State == "pending"
                                 && x.EntityId == item.EntityId))
                    {
                        dependent.State = "failed";
                        dependent.LastError = "Bağımlı oluşturma isteği başarısız oldu.";
                        failed++;
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            return new OutboxFlushResult(sent, failed, false, null);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task<OutboxExecution> ExecuteAsync(
        ApiSession session,
        SyncOutboxItem item,
        CancellationToken ct)
    {
        if (!Enum.TryParse<OutboxOperation>(item.Operation, out var operation))
            return OutboxExecution.Fail($"Bilinmeyen işlem: {item.Operation}");

        switch (operation)
        {
            case OutboxOperation.AppointmentCreate:
            case OutboxOperation.AppointmentUpdate:
            {
                var input = Deserialize<AppointmentInput>(item.Payload);
                if (input is null)
                    return OutboxExecution.Fail("Kuyruk kaydı okunamadı.");

                var result = operation == OutboxOperation.AppointmentCreate
                    ? await _client.CreateAppointmentAsync(session.BaseUrl, session.AccessToken, input, ct)
                    : await _client.UpdateAppointmentAsync(session.BaseUrl, session.AccessToken, item.EntityId, input, ct);

                if (result.Success && result.Data?.Success == true)
                    return OutboxExecution.Ok(result.Data.AppointmentId);

                return OutboxExecution.Fail(
                    result.Data?.Error ?? result.Error ?? "Sunucu kaydı kabul etmedi.",
                    result.StatusCode,
                    result.Data?.VersionConflict == true);
            }

            case OutboxOperation.AppointmentDelete:
            {
                var result = await _client.DeleteAppointmentAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    ct);

                return result.Success
                    ? OutboxExecution.Ok(null)
                    : OutboxExecution.Fail(
                        result.Error ?? "Sunucu kaydı silmedi.",
                        result.StatusCode);
            }

            case OutboxOperation.AppointmentRestore:
            {
                var result = await _client.RestoreAppointmentAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    ct);

                return result.Success
                    ? OutboxExecution.Ok(null)
                    : OutboxExecution.Fail(
                        result.Error ?? "Sunucu kaydı geri almadı.",
                        result.StatusCode);
            }

            case OutboxOperation.AppointmentOutcome:
            {
                var input = Deserialize<AppointmentOutcomeInput>(item.Payload);
                if (input is null)
                    return OutboxExecution.Fail("Kuyruk kaydı okunamadı.");

                var result = await _client.SaveOutcomeAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    input,
                    ct);

                return result.Success
                    ? OutboxExecution.Ok(null)
                    : OutboxExecution.Fail(
                        result.Error ?? "Sunucu görüşme sonucunu kabul etmedi.",
                        result.StatusCode);
            }

            case OutboxOperation.SeriesCreate:
            {
                var payload = Deserialize<SeriesOutboxPayload>(item.Payload);
                if (payload is null)
                    return OutboxExecution.Fail("Kuyruk kaydı okunamadı.");

                var result = await _client.CreateSeriesAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    payload.Input,
                    payload.Frequency,
                    payload.Count,
                    ct);

                if (result.Success && result.Data?.Success == true)
                    return OutboxExecution.Ok(result.Data.SeriesId);

                return OutboxExecution.Fail(
                    result.Data?.Error ?? result.Error ?? "Sunucu seriyi oluşturmadı.",
                    result.StatusCode);
            }

            case OutboxOperation.SeriesCancel:
            {
                var result = await _client.CancelSeriesAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    ct);

                return result.Success && result.Data?.Success == true
                    ? OutboxExecution.Ok(null)
                    : OutboxExecution.Fail(
                        result.Data?.Error ?? result.Error ?? "Sunucu seriyi iptal etmedi.",
                        result.StatusCode);
            }

            case OutboxOperation.SeriesAppend:
            {
                var payload = Deserialize<SeriesAppendOutboxPayload>(item.Payload);
                if (payload is null)
                    return OutboxExecution.Fail("Kuyruk kaydı okunamadı.");

                var result = await _client.AppendSeriesAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    payload.Count,
                    ct);

                return result.Success && result.Data?.Success == true
                    ? OutboxExecution.Ok(null)
                    : OutboxExecution.Fail(
                        result.Data?.Error ?? result.Error ?? "Sunucu seriye kayıt eklemedi.",
                        result.StatusCode);
            }

            case OutboxOperation.SeriesUpdate:
            {
                var input = Deserialize<SeriesUpdateInput>(item.Payload);
                if (input is null)
                    return OutboxExecution.Fail("Kuyruk kaydı okunamadı.");

                var result = await _client.UpdateSeriesAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    input,
                    ct);

                return result.Success && result.Data?.Success == true
                    ? OutboxExecution.Ok(null)
                    : OutboxExecution.Fail(
                        result.Data?.Error ?? result.Error ?? "Sunucu seriyi güncellemedi.",
                        result.StatusCode);
            }

            case OutboxOperation.AppointmentsBulkMove:
            {
                var payload = Deserialize<BulkMoveOutboxPayload>(item.Payload);
                if (payload is null)
                    return OutboxExecution.Fail("Kuyruk kaydı okunamadı.");

                var result = await _client.MoveAppointmentsAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    payload.Ids,
                    payload.NewDate,
                    payload.NewTime,
                    payload.IgnoreConflicts,
                    ct);

                if (!result.Success || result.Data is null)
                    return OutboxExecution.Fail(
                        result.Error ?? "Sunucu toplu taşımayı kabul etmedi.",
                        result.StatusCode);

                return result.Data.Error is not null && result.Data.Moved == 0
                    ? OutboxExecution.Fail(result.Data.Error)
                    : OutboxExecution.Ok(null);
            }

            default:
                return OutboxExecution.Fail($"Bilinmeyen işlem: {item.Operation}");
        }
    }

    private sealed record OutboxExecution(
        bool Success,
        string? ServerId,
        string? Error,
        int StatusCode,
        bool VersionConflict = false)
    {
        public bool IsNetworkError => !Success && StatusCode == 0;

        public static OutboxExecution Ok(string? serverId)
            => new(true, serverId, null, 200);

        public static OutboxExecution Fail(
            string error,
            int statusCode = 0,
            bool versionConflict = false)
            => new(false, null, error, statusCode, versionConflict);
    }

    private static T? Deserialize<T>(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(payload, ApiJson.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? Truncate(string? value)
        => value is { Length: > 1000 } ? value[..1000] : value;
}
