using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Api.Endpoints;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Reports;
using CgpRandevuTakip.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CgpRandevuTakip.Api.Tests;

public sealed class SeriesReportApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "cgp-api-series-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Data:Root", _root);
        builder.UseSetting("Jwt:Key", new string('s', 48));
        builder.UseSetting("EnableSwagger", "false");
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }
}

public class SeriesReportApiTests : IClassFixture<SeriesReportApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SeriesReportApiFactory _factory;

    public SeriesReportApiTests(SeriesReportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Reports_RequireAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/reports/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SeriesCreateAndReportSummary_WorkEndToEnd()
    {
        var client = await CreateAuthenticatedClientAsync();

        var hosts = await client.GetFromJsonAsync<List<HostDto>>("/v1/hosts", JsonOptions);
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>(
            "/v1/categories?activeOnly=true",
            JsonOptions);

        Assert.NotNull(hosts);
        Assert.NotNull(categories);
        Assert.NotEmpty(hosts!);
        Assert.NotEmpty(categories!);

        var start = DateTime.Today.AddDays(3).AddHours(10);
        var input = new AppointmentInput(
            "Api",
            "Seri",
            "05321234567",
            start,
            30,
            hosts[0].Id,
            categories[0].Id,
            AppointmentMethod.FaceToFace,
            null,
            null,
            null,
            Array.Empty<int>());

        var previewResponse = await client.PostAsJsonAsync(
            "/v1/appointments/series/preview",
            new SeriesPreviewRequest(input, SeriesFrequency.Weekly, 3),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var plan = await previewResponse.Content.ReadFromJsonAsync<SeriesPlanDto>(JsonOptions);
        Assert.NotNull(plan);
        Assert.Equal(3, plan!.CreateCount);
        Assert.True(plan.CanSave);

        var createResponse = await client.PostAsJsonAsync(
            "/v1/appointments/series",
            new SeriesCreateRequest(input, SeriesFrequency.Weekly, 3),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SeriesCreateResult>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created!.Success, created.Error);
        Assert.Equal(3, created.CreatedCount);
        Assert.NotNull(created.SeriesId);

        var summary = await GetSummaryAsync(client, DateTime.Today, DateTime.Today.AddDays(40));
        Assert.Equal(3, summary.Total);
        Assert.Equal(3, summary.Scheduled);

        var cancelResponse = await client.PostAsync(
            $"/v1/appointments/series/{created.SeriesId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<SeriesCancelResult>(JsonOptions);
        Assert.NotNull(cancelled);
        Assert.True(cancelled!.Success, cancelled.Error);
        Assert.Equal(3, cancelled.CancelledCount);

        var after = await GetSummaryAsync(client, DateTime.Today, DateTime.Today.AddDays(40));
        Assert.Equal(3, after.Cancelled);
        Assert.Equal(0, after.Scheduled);
    }

    [Fact]
    public async Task Series_RejectsInvalidCount()
    {
        var client = await CreateAuthenticatedClientAsync();

        var hosts = await client.GetFromJsonAsync<List<HostDto>>("/v1/hosts", JsonOptions);
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>(
            "/v1/categories?activeOnly=true",
            JsonOptions);

        var input = new AppointmentInput(
            "Api",
            "Gecersiz",
            "05321234568",
            DateTime.Today.AddDays(5).AddHours(11),
            30,
            hosts![0].Id,
            categories![0].Id,
            AppointmentMethod.FaceToFace,
            null,
            null,
            null,
            Array.Empty<int>());

        var response = await client.PostAsJsonAsync(
            "/v1/appointments/series",
            new SeriesCreateRequest(input, SeriesFrequency.Weekly, 1),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportSummary_RejectsTooWideRange()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            "/v1/reports/summary?from=2000-01-01&to=2026-12-31");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportKind_ReturnsTypedResult()
    {
        var client = await CreateAuthenticatedClientAsync();

        var result = await client.GetFromJsonAsync<ReportResultDto>(
            $"/v1/reports/HostPerformance?from={DateTime.Today:yyyy-MM-dd}&to={DateTime.Today.AddDays(40):yyyy-MM-dd}",
            JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(ReportKind.HostPerformance, result!.Kind);
    }

    [Fact]
    public async Task ReportKind_RejectsUnknownKind()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/v1/reports/bilinmeyen");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();

        var status = await client.GetFromJsonAsync<JsonElement>("/v1/auth/status", JsonOptions);
        if (status.GetProperty("isFirstRun").GetBoolean())
        {
            var setup = await client.PostAsJsonAsync(
                "/v1/auth/setup",
                new SetupRequestDto(
                    "admin",
                    "API Test Yönetici",
                    "api@example.com",
                    "Admin12345",
                    "Admin12345",
                    "api-series-test"),
                JsonOptions);

            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
            var tokens = await setup.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
            return client;
        }

        var login = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest("admin", "Admin12345", "api-series-test"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginTokens = await login.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginTokens!.AccessToken);
        return client;
    }

    private static async Task<ReportSummaryDto> GetSummaryAsync(
        HttpClient client,
        DateTime from,
        DateTime to)
    {
        var summary = await client.GetFromJsonAsync<ReportSummaryDto>(
            $"/v1/reports/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            JsonOptions);

        Assert.NotNull(summary);
        return summary!;
    }
}
