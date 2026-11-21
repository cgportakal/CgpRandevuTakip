using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Api.Tests;

public class SyncIdempotencyApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public SyncIdempotencyApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAppointment_SameClientRequestId_ReturnsSameAppointment()
    {
        var client = await CreateAdminClientAsync();

        var manifest = await client.GetFromJsonAsync<ContentManifestDto>(
            "/v1/content/manifest",
            JsonOptions);

        Assert.NotNull(manifest);
        var category = manifest!.Categories.First();
        var host = manifest.Hosts.First();
        var startsAt = DateTime.Today.AddDays(1).AddHours(10);

        var input = new AppointmentInput(
            "Idempotent",
            "Test",
            "+905551234567",
            startsAt,
            30,
            host.Id,
            category.Id,
            AppointmentMethod.Phone,
            null,
            null,
            null,
            new[] { 30 },
            IgnoreConflicts: false,
            ClientRequestId: "req-" + Guid.NewGuid().ToString("N"));

        var first = await client.PostAsJsonAsync("/v1/appointments", input, JsonOptions);
        var second = await client.PostAsJsonAsync("/v1/appointments", input, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstResult = await first.Content.ReadFromJsonAsync<AppointmentSaveResult>(JsonOptions);
        var secondResult = await second.Content.ReadFromJsonAsync<AppointmentSaveResult>(JsonOptions);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.True(firstResult!.Success);
        Assert.True(secondResult!.Success);
        Assert.Equal(firstResult.AppointmentId, secondResult.AppointmentId);

        var day = await client.GetFromJsonAsync<List<AppointmentListItemDto>>(
            $"/v1/appointments/day?date={startsAt:yyyy-MM-dd}",
            JsonOptions);

        Assert.NotNull(day);
        Assert.Single(day!, x => x.Id == firstResult.AppointmentId);
    }

    [Fact]
    public async Task CreateAppointment_DifferentClientRequestIds_CreatesBoth()
    {
        var client = await CreateAdminClientAsync();
        var manifest = await client.GetFromJsonAsync<ContentManifestDto>(
            "/v1/content/manifest",
            JsonOptions);

        Assert.NotNull(manifest);
        var category = manifest!.Categories.First();
        var host = manifest.Hosts.First();
        var startsAt = DateTime.Today.AddDays(2).AddHours(11);

        async Task<AppointmentSaveResult> CreateAsync(string requestId)
        {
            var input = new AppointmentInput(
                "Ayri",
                "Kayit",
                "+905551234568",
                startsAt,
                30,
                host.Id,
                category.Id,
                AppointmentMethod.Phone,
                null,
                null,
                null,
                new[] { 30 },
                IgnoreConflicts: true,
                ClientRequestId: requestId);

            var response = await client.PostAsJsonAsync("/v1/appointments", input, JsonOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<AppointmentSaveResult>(JsonOptions);
            Assert.NotNull(result);
            Assert.True(result!.Success);
            return result;
        }

        var first = await CreateAsync("req-" + Guid.NewGuid().ToString("N"));
        var second = await CreateAsync("req-" + Guid.NewGuid().ToString("N"));

        Assert.NotEqual(first.AppointmentId, second.AppointmentId);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/auth/setup",
            new SetupRequestDto(
                "admin",
                "Test Yönetici",
                "admin@example.com",
                "Admin12345",
                "Admin12345",
                "idempotency-test"),
            JsonOptions);

        // Aynı fabrika içinde başka test kurulumu yaptıysa giriş yap.
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var login = await client.PostAsJsonAsync(
                "/v1/auth/login",
                new LoginRequest("admin", "Admin12345", "idempotency-test"),
                JsonOptions);

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var loginTokens = await login.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginTokens!.AccessToken);
            return client;
        }

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }
}
