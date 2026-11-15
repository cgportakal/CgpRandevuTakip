using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/appointments")
            .WithTags("Appointments")
            .RequireAuthorization();

        group.MapGet("/day", async (DateTime? date, IAppointmentService appointments) =>
            Results.Ok(await appointments.GetDayAsync((date ?? DateTime.Today).Date)));

        group.MapGet("/search", async (
            string? search,
            string? categoryId,
            string? status,
            DateTime? from,
            DateTime? to,
            string? hostUserId,
            IAppointmentService appointments) =>
        {
            AppointmentStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<AppointmentStatus>(status, ignoreCase: true, out var value))
                    return Results.BadRequest(new ErrorResponse($"Geçersiz durum: {status}"));

                parsed = value;
            }

            return Results.Ok(await appointments.SearchAsync(
                search,
                categoryId,
                parsed,
                from,
                to,
                hostUserId));
        });

        group.MapGet("/next", async (IAppointmentService appointments) =>
        {
            var next = await appointments.GetNextAppointmentAsync();
            return next is null ? Results.NoContent() : Results.Ok(next);
        });

        group.MapGet("/deleted", async (IAppointmentService appointments) =>
            Results.Ok(await appointments.GetDeletedAsync()));

        group.MapGet("/{id}", async (string id, IAppointmentService appointments) =>
        {
            var detail = await appointments.GetDetailAsync(id);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        group.MapPost("/", async (AppointmentInput input, IAppointmentService appointments) =>
            ToResult(await appointments.CreateAsync(input)));

        group.MapPut("/{id}", async (string id, AppointmentInput input, IAppointmentService appointments) =>
            ToResult(await appointments.UpdateAsync(id, input)));

        group.MapDelete("/{id}", async (string id, IAppointmentService appointments) =>
        {
            var result = await appointments.DeleteAsync(id);
            return result.Success ? Results.NoContent() : Results.BadRequest(new ErrorResponse(result.Error ?? "Silinemedi."));
        });

        group.MapPost("/{id}/restore", async (string id, IAppointmentService appointments) =>
        {
            var result = await appointments.RestoreAsync(id);
            return result.Success ? Results.NoContent() : Results.BadRequest(new ErrorResponse(result.Error ?? "Geri alınamadı."));
        });

        group.MapGet("/{id}/outcome", async (string id, IAppointmentOutcomeService outcomes) =>
        {
            var outcome = await outcomes.GetAsync(id);
            return outcome is null ? Results.NotFound() : Results.Ok(outcome);
        });

        group.MapPut("/{id}/outcome", async (
            string id,
            AppointmentOutcomeInput input,
            IAppointmentOutcomeService outcomes) =>
        {
            var result = await outcomes.SaveAsync(id, input);
            return result.Success
                ? Results.NoContent()
                : Results.BadRequest(new ErrorResponse(result.Error ?? "Görüşme sonucu kaydedilemedi."));
        });

        return app;
    }

    private static IResult ToResult(AppointmentSaveResult result)
    {
        if (result.Success)
            return Results.Ok(result);

        if (result.Conflicts is { Count: > 0 })
            return Results.Conflict(result);

        return Results.BadRequest(result);
    }
}
