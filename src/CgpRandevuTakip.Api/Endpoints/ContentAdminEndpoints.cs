using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Api.Realtime;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Content;
using CgpRandevuTakip.Application.Hosts;

namespace CgpRandevuTakip.Api.Endpoints;

/// <summary>
/// Web içerik yönetim konsolu. Tüm uçlar yönetici yetkisi ister.
/// Düzenlemeler kaydedilir; istemcilere bildirim yalnızca yayınlama/geri alma ile gider.
/// </summary>
public static class ContentAdminEndpoints
{
    public static IEndpointRouteBuilder MapContentAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/content")
            .WithTags("Content Admin")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (IContentAdminService service) =>
            Results.Ok(await service.GetAsync()));

        group.MapPut("/settings", async (
            ContentSettingsInput input,
            IContentAdminService service) =>
            ToResult(await service.SaveSettingsAsync(input)));

        group.MapPost("/publish", async (
            PublishContentRequest request,
            IContentAdminService service,
            IRealtimeNotifier notifier) =>
        {
            var result = await service.PublishAsync(request.Note);
            if (result.Success && result.Version is not null)
                await notifier.ContentChangedAsync(
                    result.Version.VersionNumber.ToString(),
                    "published");

            return result.Success && result.Version is not null
                ? Results.Ok(result.Version)
                : Results.BadRequest(new ErrorResponse(result.Error ?? "İçerik yayınlanamadı."));
        });

        group.MapPost("/versions/{id}/restore", async (
            string id,
            IContentAdminService service,
            IRealtimeNotifier notifier) =>
        {
            var result = await service.RestoreAsync(id);
            if (result.Success && result.Version is not null)
                await notifier.ContentChangedAsync(
                    result.Version.VersionNumber.ToString(),
                    "restored");

            return result.Success && result.Version is not null
                ? Results.Ok(result.Version)
                : Results.BadRequest(new ErrorResponse(result.Error ?? "İçerik geri alınamadı."));
        });

        group.MapPost("/categories", async (
            CategoryInput input,
            ICategoryService service) =>
            ToResult(await service.CreateAsync(input)));

        group.MapPut("/categories/{id}", async (
            string id,
            CategoryInput input,
            ICategoryService service) =>
            ToResult(await service.UpdateAsync(id, input)));

        group.MapPost("/categories/{id}/active", async (
            string id,
            SetActiveRequest request,
            ICategoryService service) =>
            ToResult(await service.SetActiveAsync(id, request.IsActive)));

        group.MapPost("/categories/{id}/move", async (
            string id,
            MoveRequest request,
            ICategoryService service) =>
            ToResult(await service.MoveAsync(id, request.Direction)));

        group.MapDelete("/categories/{id}", async (
            string id,
            ICategoryService service) =>
            ToResult(await service.DeleteAsync(id)));

        group.MapPost("/hosts", async (
            HostCreateInput input,
            IHostService service) =>
            ToResult(await service.CreateAsync(input)));

        group.MapPut("/hosts/{id}", async (
            string id,
            HostUpdateInput input,
            IHostService service) =>
            ToResult(await service.UpdateAsync(id, input)));

        return app;
    }

    private static IResult ToResult(OperationResult result)
        => result.Success
            ? Results.NoContent()
            : Results.BadRequest(new ErrorResponse(result.Error ?? "İşlem başarısız."));
}

public sealed record SetActiveRequest(bool IsActive);

public sealed record MoveRequest(int Direction);
