using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? SetupUrl,
    string? PortableUrl,
    string? ReleaseUrl,
    string? Error = null)
{
    public static UpdateCheckResult Fail(string error)
        => new(false, AppConstants.Version, null, null, null, null, error);
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> InstallAsync(UpdateCheckResult update, CancellationToken cancellationToken = default);
}

/// <summary>
/// Public GitHub Releases üzerinden güncelleme kontrolü ve kurulumu.
/// Kurulu sürümde Setup sessiz çalıştırılır; taşınabilir sürümde ZIP uygulanır.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/cgportakal/CgpRandevuTakip/releases/latest";

    private static readonly HttpClient Client = CreateClient();

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(LatestReleaseApi, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.Fail($"Güncelleme sunucusuna erişilemedi ({(int)response.StatusCode}).");

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;

            var releaseUrl = root.TryGetProperty("html_url", out var htmlElement)
                ? htmlElement.GetString()
                : null;

            string? setupUrl = null;
            string? portableUrl = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                    var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                        ? urlElement.GetString()
                        : null;

                    if (url is null)
                        continue;

                    if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                        setupUrl = url;
                    else if (name.EndsWith("-portable.zip", StringComparison.OrdinalIgnoreCase))
                        portableUrl = url;
                }
            }

            var available = AppVersionComparer.IsNewer(tag, AppConstants.Version);
            return new UpdateCheckResult(
                available,
                AppConstants.Version,
                tag,
                setupUrl,
                portableUrl,
                releaseUrl);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Fail($"Güncelleme kontrol edilemedi: {ex.Message}");
        }
    }

    public async Task<OperationResult> InstallAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken = default)
    {
        if (!update.UpdateAvailable)
            return OperationResult.Ok();

        if (AppPaths.IsPortable)
        {
            if (string.IsNullOrWhiteSpace(update.PortableUrl))
                return OperationResult.Fail("Taşınabilir güncelleme paketi bulunamadı.");

            return await InstallPortableAsync(update.PortableUrl, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(update.SetupUrl))
            return OperationResult.Fail("Kurulum paketi bulunamadı.");

        try
        {
            var setupPath = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", "update", "CGP-Setup.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!);
            await DownloadAsync(update.SetupUrl, setupPath, cancellationToken);

            Process.Start(new ProcessStartInfo(setupPath)
            {
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true
            });

            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Güncelleme başlatılamadı: {ex.Message}");
        }
    }

    private static async Task<OperationResult> InstallPortableAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            var updateRoot = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", "update");
            if (Directory.Exists(updateRoot))
                Directory.Delete(updateRoot, recursive: true);
            Directory.CreateDirectory(updateRoot);

            var zipPath = Path.Combine(updateRoot, "portable.zip");
            await DownloadAsync(url, zipPath, cancellationToken);

            var extractPath = Path.Combine(updateRoot, "files");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);

            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var scriptPath = Path.Combine(updateRoot, "apply-update.cmd");
            var exeName = "CgpRandevuTakip.Desktop.exe";

            var script = $"""
                @echo off
                timeout /t 3 /nobreak >nul
                robocopy "{extractPath}" "{appDir}" /E /R:2 /W:1 /NFL /NDL /NJH /NJS
                start "" "{Path.Combine(appDir, exeName)}"
                del "%~f0"
                """;

            File.WriteAllText(scriptPath, script);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Taşınabilir güncelleme hazırlanamadı: {ex.Message}");
        }
    }

    private static async Task DownloadAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CGP-RandevuTakip-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
