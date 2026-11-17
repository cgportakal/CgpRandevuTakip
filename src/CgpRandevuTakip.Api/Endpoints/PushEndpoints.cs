using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Api.Notifications;
using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Api.Endpoints;

public static class PushEndpoints
{
    private static readonly string[] Platforms = { "android", "ios", "web" };

    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        // Web Push abonelikleri için genel anahtar; kimlik doğrulaması gerekmez.
        app.MapGet("/v1/push/public-key", (VapidKeyProvider vapid) =>
                Results.Ok(new PushPublicKeyResponse(vapid.PublicKey, vapid.Subject)))
            .AllowAnonymous()
            .WithTags("Push");

        var devices = app.MapGroup("/v1/devices")
            .WithTags("Push")
            .RequireAuthorization();

        devices.MapGet("/", async (ICurrentUser currentUser, DeviceStore store) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var rows = await store.ListAsync(currentUser.UserId);
            return Results.Ok(rows.Select(row => new DeviceDto(
                row.Id,
                row.Platform,
                row.AppVersion,
                row.UpdatedAtUtc)));
        });

        devices.MapPost("/", async (
            DeviceRegistrationRequest request,
            ICurrentUser currentUser,
            DeviceStore store) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var platform = request.Platform?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!Platforms.Contains(platform))
                return Results.BadRequest(new ErrorResponse("Geçersiz platform. android, ios veya web olmalıdır."));

            if (platform == "web")
            {
                if (string.IsNullOrWhiteSpace(request.Endpoint)
                    || string.IsNullOrWhiteSpace(request.P256dh)
                    || string.IsNullOrWhiteSpace(request.Auth))
                {
                    return Results.BadRequest(new ErrorResponse("Web push için endpoint, p256dh ve auth zorunludur."));
                }
            }
            else if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.BadRequest(new ErrorResponse("Mobil push için cihaz belirteci zorunludur."));
            }

            var record = await store.UpsertAsync(currentUser.UserId, request);
            return Results.Ok(new DeviceDto(record.Id, record.Platform, record.AppVersion, record.UpdatedAtUtc));
        });

        devices.MapDelete("/", async (
            string? token,
            string? endpoint,
            ICurrentUser currentUser,
            DeviceStore store) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(endpoint))
                return Results.BadRequest(new ErrorResponse("Silinecek cihaz belirteci veya endpoint gerekli."));

            await store.DeleteAsync(currentUser.UserId, token, endpoint);
            return Results.NoContent();
        });

        app.MapPost("/v1/push/test", async (
                ICurrentUser currentUser,
                DeviceStore store,
                IDevicePushSender sender) =>
            {
                if (currentUser.UserId is null) return Results.Unauthorized();

                var devices = await store.ListAsync(currentUser.UserId);
                var sent = await sender.SendToUserAsync(
                    currentUser.UserId,
                    "CGP - Randevu Takip",
                    "Test bildirimi: push bağlantısı çalışıyor.",
                    new Dictionary<string, string> { ["type"] = "test" });

                return Results.Ok(new PushTestResponse(sent, devices.Count));
            })
            .RequireAuthorization()
            .WithTags("Push");

        return app;
    }
}
