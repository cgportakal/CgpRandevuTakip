using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Content;
using CgpRandevuTakip.Application.Hosts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CgpRandevuTakip.Api.Tests;

public sealed class ContentAdminApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "cgp-api-content-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Data:Root", _root);
        builder.UseSetting("Jwt:Key", new string('c', 48));
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

public class ContentAdminApiTests : IClassFixture<ContentAdminApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ContentAdminApiFactory _factory;

    public ContentAdminApiTests(ContentAdminApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ContentAdmin_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/admin/content");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ContentAdmin_EditPublishAndRestore_WorkEndToEnd()
    {
        var client = await CreateAuthenticatedClientAsync();

        var initial = await client.GetFromJsonAsync<ContentAdminDto>("/v1/admin/content", JsonOptions);
        Assert.NotNull(initial);
        Assert.NotEmpty(initial!.Categories);
        Assert.Null(initial.CurrentVersion);

        var categoryName = $"İçerik Test {Guid.NewGuid():N}"[..22];
        var createCategory = await client.PostAsJsonAsync(
            "/v1/admin/content/categories",
            new CategoryInput(categoryName, "#2E7D5B", true),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, createCategory.StatusCode);

        var settings = new ContentSettingsInput(
            "Test Kurum",
            "test@example.com",
            "0532 123 45 67",
            "Test Mahallesi No 1",
            30,
            "06:00",
            "20:45",
            false,
            true,
            false,
            true,
            "22:00",
            "07:00",
            "08:30");

        var saveSettings = await client.PutAsJsonAsync(
            "/v1/admin/content/settings",
            settings,
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, saveSettings.StatusCode);

        var createHost = await client.PostAsJsonAsync(
            "/v1/admin/content/hosts",
            new HostCreateInput("Test Görüşmeci", false, null, null, null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, createHost.StatusCode);

        var afterEdits = await client.GetFromJsonAsync<ContentAdminDto>("/v1/admin/content", JsonOptions);
        Assert.NotNull(afterEdits);
        var category = Assert.Single(afterEdits!.Categories, item => item.Name == categoryName);
        var host = Assert.Single(afterEdits.Hosts, item => item.DisplayName == "Test Görüşmeci");
        Assert.Equal("Test Kurum", afterEdits.Institution.Name);

        var v1 = await PublishAsync(client, "İlk yayın");
        Assert.Equal(1, v1.VersionNumber);

        var updateCategory = await client.PutAsJsonAsync(
            $"/v1/admin/content/categories/{category.Id}",
            new CategoryInput($"{categoryName} Güncel", "#123456", true),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, updateCategory.StatusCode);

        var updateSettings = await client.PutAsJsonAsync(
            "/v1/admin/content/settings",
            settings with { InstitutionName = "Test Kurum Güncel" },
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, updateSettings.StatusCode);

        var v2 = await PublishAsync(client, "İkinci yayın");
        Assert.Equal(2, v2.VersionNumber);

        var restore = await client.PostAsync(
            $"/v1/admin/content/versions/{v1.Id}/restore",
            content: null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var restored = await restore.Content.ReadFromJsonAsync<ContentVersionDto>(JsonOptions);
        Assert.NotNull(restored);
        Assert.Equal(3, restored!.VersionNumber);

        var afterRestore = await client.GetFromJsonAsync<ContentAdminDto>("/v1/admin/content", JsonOptions);
        Assert.NotNull(afterRestore);
        Assert.Equal("Test Kurum", afterRestore!.Institution.Name);
        Assert.Contains(afterRestore.Categories, item => item.Name == categoryName);
        Assert.DoesNotContain(afterRestore.Categories, item => item.Name == $"{categoryName} Güncel");
        Assert.True(afterRestore.CurrentVersion?.IsCurrent);
        Assert.Equal(3, afterRestore.CurrentVersion?.VersionNumber);
        Assert.True(afterRestore.Versions.Count >= 3);

        var manifest = await client.GetFromJsonAsync<ContentManifestDto>("/v1/content/manifest", JsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal("Test Kurum", manifest!.Institution.Name);
        Assert.Contains(manifest.Categories, item => item.Name == categoryName);

        var inactiveHost = await client.PutAsJsonAsync(
            $"/v1/admin/content/hosts/{host.Id}",
            new HostUpdateInput("Test Görüşmeci", false),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, inactiveHost.StatusCode);

        var afterHostEdit = await client.GetFromJsonAsync<ContentAdminDto>("/v1/admin/content", JsonOptions);
        Assert.NotNull(afterHostEdit);
        Assert.False(Assert.Single(afterHostEdit!.Hosts, item => item.Id == host.Id).IsActive);

        var logoBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        using var logoForm = new MultipartFormDataContent();
        var logoContent = new ByteArrayContent(logoBytes);
        logoContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        logoForm.Add(logoContent, "file", "institution-logo.png");

        var uploadLogo = await client.PostAsync("/v1/admin/content/logo", logoForm);
        Assert.Equal(HttpStatusCode.OK, uploadLogo.StatusCode);

        var afterLogo = await client.GetFromJsonAsync<ContentAdminDto>("/v1/admin/content", JsonOptions);
        Assert.NotNull(afterLogo);
        Assert.False(string.IsNullOrWhiteSpace(afterLogo!.Institution.LogoUrl));

        var logoResponse = await client.GetAsync(afterLogo.Institution.LogoUrl);
        Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
        Assert.Equal("image/png", logoResponse.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();

        var setup = await client.PostAsJsonAsync(
            "/v1/auth/setup",
            new SetupRequestDto(
                "admin",
                "Test Yönetici",
                "admin@example.com",
                "Admin12345",
                "Admin12345",
                "content-api-test"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        var tokens = await setup.Content.ReadFromJsonAsync<AuthTokensResponse>(JsonOptions);
        Assert.NotNull(tokens);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }

    private static async Task<ContentVersionDto> PublishAsync(HttpClient client, string note)
    {
        var response = await client.PostAsJsonAsync(
            "/v1/admin/content/publish",
            new PublishContentRequest(note),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<ContentVersionDto>(JsonOptions);
        Assert.NotNull(version);
        return version!;
    }
}
