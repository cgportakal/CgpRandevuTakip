using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;

namespace CgpRandevuTakip.Infrastructure.Google;

/// <summary>Google Drive REST API'siyle yedek yükleme. OAuth yenileme token'ı ayarlarda saklanır.</summary>
public sealed class GoogleDriveBackupUploader : IGoogleDriveBackupUploader
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ISettingsService _settings;

    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc;

    public GoogleDriveBackupUploader(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<OperationResult> UploadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync();
        if (!settings.GoogleDriveEnabled)
            return OperationResult.Ok();

        if (string.IsNullOrWhiteSpace(settings.GoogleClientId)
            || string.IsNullOrWhiteSpace(settings.GoogleClientSecret)
            || string.IsNullOrWhiteSpace(settings.GoogleRefreshToken))
        {
            return OperationResult.Fail("Google Drive bağlantısı yapılandırılmamış.");
        }

        try
        {
            var token = await GetAccessTokenAsync(settings, cancellationToken);
            if (token is null)
                return OperationResult.Fail("Google Drive oturumu yenilenemedi.");

            var folderId = await EnsureFolderAsync(token, settings.GoogleFolderName, cancellationToken);
            var fileName = Path.GetFileName(filePath);

            await using var stream = File.OpenRead(filePath);
            using var form = new MultipartFormDataContent();
            var metadata = JsonSerializer.Serialize(new { name = fileName, parents = new[] { folderId } });
            form.Add(new StringContent(metadata, Encoding.UTF8, "application/json"), "metadata");
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = form;

            using var response = await Client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return OperationResult.Fail($"Google Drive yükleme hatası ({(int)response.StatusCode}): {body}");
            }

            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Google Drive yükleme hatası: {ex.Message}");
        }
    }

    private async Task<string?> GetAccessTokenAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _accessTokenExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
            return _accessToken;

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = settings.GoogleClientId,
            ["client_secret"] = settings.GoogleClientSecret,
            ["refresh_token"] = settings.GoogleRefreshToken,
            ["grant_type"] = "refresh_token"
        });

        using var response = await Client.PostAsync(
            "https://oauth2.googleapis.com/token",
            form,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("access_token", out var tokenElement))
            return null;

        _accessToken = tokenElement.GetString();
        var expires = document.RootElement.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 3600;
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(expires);
        return _accessToken;
    }

    private static async Task<string> EnsureFolderAsync(
        string accessToken,
        string folderName,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(folderName) ? "CGP Yedekler" : folderName.Trim();
        var query = Uri.EscapeDataString(
            $"name = '{name.Replace("'", "\\'")}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false and 'root' in parents");

        using (var listRequest = new HttpRequestMessage(
                   HttpMethod.Get,
                   $"https://www.googleapis.com/drive/v3/files?q={query}&fields=files(id,name)&pageSize=1"))
        {
            listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var listResponse = await Client.SendAsync(listRequest, cancellationToken);
            if (listResponse.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(
                    await listResponse.Content.ReadAsStringAsync(cancellationToken));
                if (document.RootElement.TryGetProperty("files", out var files)
                    && files.GetArrayLength() > 0
                    && files[0].TryGetProperty("id", out var existingId))
                {
                    return existingId.GetString()!;
                }
            }
        }

        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://www.googleapis.com/drive/v3/files?fields=id");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createRequest.Content = JsonContent.Create(new
        {
            name,
            mimeType = "application/vnd.google-apps.folder",
            parents = new[] { "root" }
        });

        using var createResponse = await Client.SendAsync(createRequest, cancellationToken);
        var createBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Klasör oluşturulamadı: {createBody}");

        using var created = JsonDocument.Parse(createBody);
        return created.RootElement.GetProperty("id").GetString()!;
    }
}
