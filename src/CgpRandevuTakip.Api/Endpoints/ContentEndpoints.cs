using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Api.Infrastructure;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Settings;

namespace CgpRandevuTakip.Api.Endpoints;

public static class ContentEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/content")
            .WithTags("Content")
            .RequireAuthorization();

        group.MapGet("/manifest", async (
            HttpContext context,
            ISettingsService settingsService,
            ICategoryService categories,
            IHostService hosts,
            IConfiguration configuration) =>
        {
            var settings = await settingsService.GetAsync();
            var activeCategories = await categories.GetActiveAsync();
            var selectableHosts = await hosts.GetSelectableHostsAsync();

            var institution = new InstitutionDto(
                settings.InstitutionName,
                settings.InstitutionEmail,
                settings.InstitutionPhone,
                settings.InstitutionAddress,
                ResolveLogoUrl(settings.InstitutionLogoFile));

            var publicSettings = new PublicSettingsDto(
                settings.Theme.ToString(),
                settings.DefaultReminderMinutes,
                settings.WorkHoursStart,
                settings.WorkHoursEnd,
                settings.AllowOutOfHoursAppointments,
                settings.NotificationsEnabled,
                settings.PushEnabled,
                settings.QuietHoursEnabled,
                settings.QuietHoursStart,
                settings.QuietHoursEnd,
                settings.DailyShareTime);

            var features = configuration.GetSection("Features").Get<Dictionary<string, bool>>()
                           ?? new Dictionary<string, bool>();

            var policy = configuration.GetSection("VersionPolicy").Get<VersionPolicyDto>()
                         ?? new VersionPolicyDto("1.5.0", "1.0.0", "1.0.0", "1.5.1", "1.0.0");

            var core = new
            {
                institution,
                settings = publicSettings,
                categories = activeCategories,
                hosts = selectableHosts,
                features,
                versionPolicy = policy
            };

            var json = JsonSerializer.Serialize(core, JsonOptions);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
                .ToLowerInvariant()[..16];

            var etag = $"\"{hash}\"";
            context.Response.Headers.ETag = etag;

            if (context.Request.Headers.IfNoneMatch.ToString() == etag)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return Results.Ok(new ContentManifestDto(
                hash,
                DateTime.UtcNow,
                institution,
                publicSettings,
                activeCategories,
                selectableHosts,
                features,
                policy));
        });

        group.MapGet("/logo", (ApiEnvironment environment) =>
        {
            var path = FindLogo(environment);
            if (path is null)
                return Results.NotFound();

            var contentType = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };

            return Results.File(path, contentType);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/health", () => Results.Ok(new
            {
                status = "ok",
                version = Domain.Common.AppConstants.Version,
                schemaVersion = Domain.Common.AppConstants.SchemaVersion,
                serverTimeUtc = DateTime.UtcNow
            }))
            .AllowAnonymous()
            .WithTags("System");

        app.MapGet("/", () => Results.Ok(new
            {
                name = "CGP - Randevu Takip API",
                version = Domain.Common.AppConstants.Version,
                documentation = "/swagger"
            }))
            .AllowAnonymous();

        return app;
    }

    private static string? ResolveLogoUrl(string logoFile)
    {
        if (string.IsNullOrWhiteSpace(logoFile))
            return null;

        return "/v1/content/logo";
    }

    private static string? FindLogo(ApiEnvironment environment)
    {
        if (!Directory.Exists(environment.BrandingDirectory))
            return null;

        var files = Directory.GetFiles(environment.BrandingDirectory, "institution-logo.*");
        return files.FirstOrDefault();
    }
}
