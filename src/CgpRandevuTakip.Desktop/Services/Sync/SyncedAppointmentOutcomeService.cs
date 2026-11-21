using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Desktop.Services.Api;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Görüşme sonucu okuma/yazma yolu. Sunucu okuma modu açıkken sonuç API'ye gider;
/// bağlantı yoksa outbox kuyruğuna alınır.
/// </summary>
public sealed class SyncedAppointmentOutcomeService : IAppointmentOutcomeService
{
    private readonly IAppointmentOutcomeService _local;
    private readonly ServerReadService _reads;
    private readonly ApiConnectionService _connection;
    private readonly CgpApiClient _client;
    private readonly OutboxService _outbox;

    public SyncedAppointmentOutcomeService(
        IAppointmentOutcomeService local,
        ServerReadService reads,
        ApiConnectionService connection,
        CgpApiClient client,
        OutboxService outbox)
    {
        _local = local;
        _reads = reads;
        _connection = connection;
        _client = client;
        _outbox = outbox;
    }

    public async Task<AppointmentOutcomeDto?> GetAsync(string appointmentId)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.GetAsync(appointmentId);

        var detail = await _reads.GetDetailAsync(appointmentId);
        return detail.Success
            ? detail.Data?.Outcome
            : await _local.GetAsync(appointmentId);
    }

    public async Task<OperationResult> SaveAsync(string appointmentId, AppointmentOutcomeInput input)
    {
        if (!await _reads.IsServerReadActiveAsync())
            return await _local.SaveAsync(appointmentId, input);

        var session = await _connection.GetAuthorizedSessionAsync();
        if (session is null)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentOutcome, appointmentId, input);
            return OperationResult.QueuedOk();
        }

        var result = await _client.SaveOutcomeAsync(
            session.BaseUrl,
            session.AccessToken,
            appointmentId,
            input);

        if (result.Success)
            return OperationResult.Ok();

        if (result.IsNetworkError)
        {
            await _outbox.EnqueueAsync(OutboxOperation.AppointmentOutcome, appointmentId, input);
            return OperationResult.QueuedOk();
        }

        return OperationResult.Fail(result.Error ?? "Görüşme sonucu sunucuya kaydedilemedi.");
    }
}
