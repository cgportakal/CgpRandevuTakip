using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;

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

        return app;
    }
}
