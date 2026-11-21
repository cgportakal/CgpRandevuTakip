using System.Text.Json;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Desktop.Services.Sync;

public enum OutboxOperation
{
    AppointmentCreate = 0,
    AppointmentUpdate = 1,
    AppointmentDelete = 2,
    AppointmentOutcome = 3
}

public sealed record OutboxFlushResult(
    int Sent,
    int Failed,
    bool Offline,
    string? Error)
{
    public static OutboxFlushResult Empty { get; } = new(0, 0, false, null);
}

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

                item.State = "failed";
                failed++;
            }

            await db.SaveChangesAsync(ct);
            return new OutboxFlushResult(sent, failed, false, null);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task<ApiResult<object>> ExecuteAsync(
        ApiSession session,
        SyncOutboxItem item,
        CancellationToken ct)
    {
        if (!Enum.TryParse<OutboxOperation>(item.Operation, out var operation))
            return ApiResult<object>.Fail($"Bilinmeyen işlem: {item.Operation}");

        switch (operation)
        {
            case OutboxOperation.AppointmentCreate:
            case OutboxOperation.AppointmentUpdate:
            {
                var input = Deserialize<AppointmentInput>(item.Payload);
                if (input is null)
                    return ApiResult<object>.Fail("Kuyruk kaydı okunamadı.");

                var result = operation == OutboxOperation.AppointmentCreate
                    ? await _client.CreateAppointmentAsync(session.BaseUrl, session.AccessToken, input, ct)
                    : await _client.UpdateAppointmentAsync(session.BaseUrl, session.AccessToken, item.EntityId, input, ct);

                if (result.Success && result.Data?.Success == true)
                    return ApiResult<object>.Ok(new object());

                return ApiResult<object>.Fail(
                    result.Data?.Error ?? result.Error ?? "Sunucu kaydı kabul etmedi.",
                    result.StatusCode);
            }

            case OutboxOperation.AppointmentDelete:
            {
                var result = await _client.DeleteAppointmentAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    ct);

                return result.Success
                    ? ApiResult<object>.Ok(new object())
                    : ApiResult<object>.Fail(
                        result.Error ?? "Sunucu kaydı silmedi.",
                        result.StatusCode);
            }

            case OutboxOperation.AppointmentOutcome:
            {
                var input = Deserialize<AppointmentOutcomeInput>(item.Payload);
                if (input is null)
                    return ApiResult<object>.Fail("Kuyruk kaydı okunamadı.");

                var result = await _client.SaveOutcomeAsync(
                    session.BaseUrl,
                    session.AccessToken,
                    item.EntityId,
                    input,
                    ct);

                return result.Success
                    ? ApiResult<object>.Ok(new object())
                    : ApiResult<object>.Fail(
                        result.Error ?? "Sunucu görüşme sonucunu kabul etmedi.",
                        result.StatusCode);
            }

            default:
                return ApiResult<object>.Fail($"Bilinmeyen işlem: {item.Operation}");
        }
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
