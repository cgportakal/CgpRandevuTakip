using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Rooms;
using CgpRandevuTakip.Application.Titles;

namespace CgpRandevuTakip.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var categories = app.MapGroup("/v1/categories")
            .WithTags("Categories")
            .RequireAuthorization();

        categories.MapGet("/", async (ICategoryService service, bool? activeOnly) =>
            Results.Ok(activeOnly == true
                ? await service.GetActiveAsync()
                : await service.GetAllAsync()));

        var hosts = app.MapGroup("/v1/hosts")
            .WithTags("Hosts")
            .RequireAuthorization();

        hosts.MapGet("/", async (IHostService service) =>
            Results.Ok(await service.GetSelectableHostsAsync()));

        var titles = app.MapGroup("/v1/titles")
            .WithTags("Titles")
            .RequireAuthorization();

        titles.MapGet("/", async (IHostTitleService service, bool? activeOnly) =>
            Results.Ok(activeOnly == false
                ? await service.GetAllAsync()
                : await service.GetActiveAsync()));

        var rooms = app.MapGroup("/v1/rooms")
            .WithTags("Rooms")
            .RequireAuthorization();

        rooms.MapGet("/", async (
            IMeetingRoomService service,
            bool? activeOnly,
            string? hostUserId) =>
            Results.Ok(activeOnly == false
                ? await service.GetAllAsync()
                : await service.GetActiveAsync(hostUserId)));

        return app;
    }
}
