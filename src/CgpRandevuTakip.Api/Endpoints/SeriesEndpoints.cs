using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Api.Realtime;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Api.Endpoints;

/// <summary>Tekrarlayan görüşme serisi uçları. Yetki kuralları uygulama servisinde uygulanır.</summary>
public static class SeriesEndpoints
{
    public static IEndpointRouteBuilder MapSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/appointments/series")
            .WithTags("Series")
            .RequireAuthorization();

        group.MapPost("/preview", async (
            SeriesPreviewRequest request,
            IAppointmentService appointments) =>
        {
            var plan = await appointments.PreviewSeriesAsync(
                request.Input,
                new SeriesRequest(request.Frequency, request.Count));

            return plan.Error is not null && plan.Slots.Count == 0
                ? Results.BadRequest(new ErrorResponse(plan.Error))
                : Results.Ok(plan);
        });

        group.MapPost("/", async (
            SeriesCreateRequest request,
            IAppointmentService appointments,
            IRealtimeNotifier notifier) =>
        {
            var result = await appointments.CreateSeriesAsync(
                request.Input,
                new SeriesRequest(request.Frequency, request.Count));

            if (result.Success && result.SeriesId is not null)
                await notifier.SeriesChangedAsync(result.SeriesId, "created");

            if (result.Success)
                return Results.Ok(result);

            return result.Slots.Count > 0
                ? Results.Conflict(result)
                : Results.BadRequest(result);
        });

        group.MapPost("/{seriesId}/cancel", async (
            string seriesId,
            IAppointmentService appointments,
            IRealtimeNotifier notifier) =>
        {
            var result = await appointments.CancelSeriesAsync(seriesId);
            if (result.Success)
                await notifier.SeriesChangedAsync(seriesId, "cancelled");

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new ErrorResponse(result.Error ?? "Seri iptal edilemedi."));
        });

        group.MapPost("/{seriesId}/append", async (
            string seriesId,
            SeriesAppendRequestDto request,
            IAppointmentService appointments,
            IRealtimeNotifier notifier) =>
        {
            var result = await appointments.AppendSeriesAsync(seriesId, request.Count);
            if (result.Success)
                await notifier.SeriesChangedAsync(seriesId, "extended");

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new ErrorResponse(result.Error ?? "Seriye kayıt eklenemedi."));
        });

        group.MapPut("/{seriesId}", async (
            string seriesId,
            SeriesUpdateInput input,
            IAppointmentService appointments,
            IRealtimeNotifier notifier) =>
        {
            var result = await appointments.UpdateSeriesAsync(seriesId, input);
            if (result.Success)
                await notifier.SeriesChangedAsync(seriesId, "updated");

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new ErrorResponse(result.Error ?? "Seri güncellenemedi."));
        });

        return app;
    }
}

public sealed record SeriesAppendRequestDto(int Count);

public sealed record SeriesPreviewRequest(
    AppointmentInput Input,
    SeriesFrequency Frequency,
    int Count);

public sealed record SeriesCreateRequest(
    AppointmentInput Input,
    SeriesFrequency Frequency,
    int Count);
