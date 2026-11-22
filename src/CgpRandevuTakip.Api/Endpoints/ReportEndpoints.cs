using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Application.Reports;

namespace CgpRandevuTakip.Api.Endpoints;

/// <summary>Rapor uçları. Görüşmeci rolü yalnızca kendi görüşmelerini görür (servis kapsamı).</summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/summary", async (
            DateTime? from,
            DateTime? to,
            string? categoryId,
            string? hostUserId,
            string? roomId,
            IReportService reports) =>
        {
            var toDate = (to ?? DateTime.Today).Date;
            var fromDate = (from ?? toDate.AddDays(-29)).Date;

            if ((toDate - fromDate).TotalDays > 3660)
                return Results.BadRequest(new ErrorResponse("Rapor aralığı en fazla 10 yıl olabilir."));

            var query = new ReportQuery(fromDate, toDate, categoryId, hostUserId, roomId);
            return Results.Ok(await reports.GetSummaryAsync(query));
        });

        group.MapGet("/kind/{kind}", async (
            string kind,
            DateTime? from,
            DateTime? to,
            string? categoryId,
            string? hostUserId,
            string? roomId,
            string? personSearch,
            IReportService reports) =>
        {
            if (!Enum.TryParse<ReportKind>(kind, ignoreCase: true, out var parsed))
                return Results.BadRequest(new ErrorResponse($"Geçersiz rapor türü: {kind}"));

            var toDate = (to ?? DateTime.Today).Date;
            var fromDate = (from ?? toDate.AddDays(-29)).Date;

            if ((toDate - fromDate).TotalDays > 3660)
                return Results.BadRequest(new ErrorResponse("Rapor aralığı en fazla 10 yıl olabilir."));

            var query = new ReportQuery(fromDate, toDate, categoryId, hostUserId, roomId);
            return Results.Ok(await reports.GetReportAsync(parsed, query, personSearch));
        });

        return app;
    }
}
