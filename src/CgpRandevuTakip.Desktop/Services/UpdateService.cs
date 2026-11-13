using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Updates;
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
    string? Error = null,
    string? SetupSha256 = null,
    string? PortableSha256 = null,
    string? ReleaseNotes = null)
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
/// Public GitHub Releases üzerinden güncelleme denetimi ve kurulumu.
/// Paketler SHA-256 ile doğrulanır; kurulum öncesi geri dönüş anlık görüntüsü alınır.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/cgportakal/CgpRandevuTakip/releases/latest";

    private static readonly HttpClient SharedClient = CreateClient();
    private readonly UpdateRollbackService _rollback;
    private readonly HttpClient _client;
    private readonly string _latestReleaseApi;

    public UpdateService(UpdateRollbackService rollback)
        : this(rollback, SharedClient, LatestReleaseApi)
    {
    }

    public UpdateService(UpdateRollbackService rollback, HttpClient client, string latestReleaseApi)
    {
        _rollback = rollback;
        _client = client;
        _latestReleaseApi = latestReleaseApi;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateLog.Write("Güncelleme denetimi başladı.");

            using var response = await _client.GetAsync(_latestReleaseApi, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = $"Güncelleme sunucusuna erişilemedi ({(int)response.StatusCode}).";
                UpdateLog.Write(error);
                return UpdateCheckResult.Fail(error);
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;

            var releaseUrl = root.TryGetProperty("html_url", out var htmlElement)
                ? htmlElement.GetString()
                : null;

            var releaseNotes = root.TryGetProperty("body", out var bodyElement)
                ? bodyElement.GetString()
                : null;

            string? setupUrl = null;
            string? portableUrl = null;
            string? setupDigest = null;
            string? portableDigest = null;

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

                    var digest = asset.TryGetProperty("digest", out var digestElement)
                        ? UpdateIntegrity.NormalizeDigest(digestElement.GetString())
                        : null;

                    if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl = url;
                        setupDigest = digest;
                    }
                    else if (name.EndsWith("-portable.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        portableUrl = url;
                        portableDigest = digest;
                    }
                }
            }

            var available = AppVersionComparer.IsNewer(tag, AppConstants.Version);
            UpdateLog.Write(available
                ? $"Yeni sürüm bulundu: {tag} (mevcut {AppConstants.Version})."
                : $"Güncel sürüm: {AppConstants.Version}.");

            return new UpdateCheckResult(
                available,
                AppConstants.Version,
                tag,
                setupUrl,
                portableUrl,
                releaseUrl,
                Error: null,
                SetupSha256: setupDigest,
                PortableSha256: portableDigest,
                ReleaseNotes: releaseNotes);
        }
        catch (Exception ex)
        {
            var error = $"Güncelleme kontrol edilemedi: {ex.Message}";
            UpdateLog.Write(error);
            return UpdateCheckResult.Fail(error);
        }
    }

    public async Task<OperationResult> InstallAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken = default)
    {
        if (!update.UpdateAvailable)
            return OperationResult.Ok();

        if (string.IsNullOrWhiteSpace(update.LatestVersion))
            return OperationResult.Fail("Güncelleme sürümü belirlenemedi.");

        return AppPaths.IsPortable
            ? await InstallPortableAsync(update, cancellationToken)
            : await InstallSetupAsync(update, cancellationToken);
    }

    private async Task<OperationResult> InstallSetupAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(update.SetupUrl))
            return OperationResult.Fail("Kurulum paketi bulunamadı.");

        if (string.IsNullOrWhiteSpace(update.SetupSha256))
            return OperationResult.Fail("Kurulum paketinin SHA-256 özeti bulunamadı; güvenlik için güncelleme durduruldu.");

        var setupPath = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            "update",
            $"CGP-Setup-{update.LatestVersion}.exe");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!);

            UpdateLog.Write($"Kurulum paketi indiriliyor: {update.SetupUrl}");
            await DownloadAsync(update.SetupUrl, setupPath, cancellationToken);

            if (!UpdateIntegrity.Matches(setupPath, update.SetupSha256))
            {
                SafeDelete(setupPath);
                UpdateLog.Write("Kurulum paketi SHA-256 doğrulaması başarısız.");
                return OperationResult.Fail("Kurulum paketi doğrulanamadı. İndirme bozuk olabilir; tekrar deneyin.");
            }

            UpdateLog.Write("Kurulum paketi doğrulandı.");

            if (!_rollback.Prepare(update.LatestVersion!, portable: false))
                return OperationResult.Fail("Geri dönüş dosyaları hazırlanamadı; güvenlik için güncelleme durduruldu.");

            Process.Start(new ProcessStartInfo(setupPath)
            {
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true
            });

            UpdateLog.Write("Kurulum başlatıldı; uygulama kapanacak.");
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            UpdateLog.Write($"Kurulum başlatılamadı: {ex.Message}");
            return OperationResult.Fail($"Güncelleme başlatılamadı: {ex.Message}");
        }
    }

    private async Task<OperationResult> InstallPortableAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(update.PortableUrl))
            return OperationResult.Fail("Taşınabilir güncelleme paketi bulunamadı.");

        if (string.IsNullOrWhiteSpace(update.PortableSha256))
            return OperationResult.Fail("Güncelleme paketinin SHA-256 özeti bulunamadı; güvenlik için güncelleme durduruldu.");

        try
        {
            var updateRoot = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", "update");
            if (Directory.Exists(updateRoot))
                Directory.Delete(updateRoot, recursive: true);
            Directory.CreateDirectory(updateRoot);

            var zipPath = Path.Combine(updateRoot, $"portable-{update.LatestVersion}.zip");
            UpdateLog.Write($"Taşınabilir paket indiriliyor: {update.PortableUrl}");
            await DownloadAsync(update.PortableUrl, zipPath, cancellationToken);

            if (!UpdateIntegrity.Matches(zipPath, update.PortableSha256))
            {
                SafeDelete(zipPath);
                UpdateLog.Write("Taşınabilir paket SHA-256 doğrulaması başarısız.");
                return OperationResult.Fail("Güncelleme paketi doğrulanamadı. İndirme bozuk olabilir; tekrar deneyin.");
            }

            UpdateLog.Write("Taşınabilir paket doğrulandı.");

            var extractPath = Path.Combine(updateRoot, "files");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            if (!_rollback.Prepare(update.LatestVersion!, portable: true))
                return OperationResult.Fail("Geri dönüş dosyaları hazırlanamadı; güvenlik için güncelleme durduruldu.");

            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var scriptPath = Path.Combine(updateRoot, "apply-update.cmd");
            var exePath = Path.Combine(appDir, "CgpRandevuTakip.Desktop.exe");
            var dataDir = Path.Combine(appDir, "CGP-Data");
            var resultFile = Path.Combine(_rollback.UpdatesRoot, "update-result.txt");
            var pendingFile = Path.Combine(_rollback.UpdatesRoot, "pending.json");

            var script = $"""
                @echo off
                timeout /t 2 /nobreak >nul
                :waitloop
                tasklist /fi "imagename eq CgpRandevuTakip.Desktop.exe" | find /i "CgpRandevuTakip.Desktop.exe" >nul
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >nul
                    goto waitloop
                )
                robocopy "{extractPath}" "{appDir}" /E /R:3 /W:1 /NFL /NDL /NJH /NJS /XD "{dataDir}"
                if errorlevel 8 (
                    robocopy "{_rollback.SnapshotAppDirectory}" "{appDir}" /E /R:3 /W:1 /NFL /NDL /NJH /NJS /XD "{dataDir}"
                    echo ROLLBACK>"{resultFile}"
                ) else (
                    echo SUCCESS>"{resultFile}"
                )
                del "{pendingFile}" >nul 2>&1
                start "" "{exePath}"
                del "%~f0"
                """;

            File.WriteAllText(scriptPath, script);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            UpdateLog.Write("Taşınabilir güncelleme betiği başlatıldı; uygulama kapanacak.");
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            UpdateLog.Write($"Taşınabilir güncelleme hazırlanamadı: {ex.Message}");
            return OperationResult.Fail($"Taşınabilir güncelleme hazırlanamadı: {ex.Message}");
        }
    }

    /// <summary>
    /// Taşınabilir güncelleme betiğinin sonucunu bir sonraki açılışta bildirir.
    /// </summary>
    public void ReportPortableResult()
    {
        try
        {
            var resultFile = Path.Combine(_rollback.UpdatesRoot, "update-result.txt");
            if (!File.Exists(resultFile))
                return;

            var result = File.ReadAllText(resultFile).Trim();
            File.Delete(resultFile);

            if (string.Equals(result, "ROLLBACK", StringComparison.OrdinalIgnoreCase))
            {
                UpdateLog.Write("Taşınabilir güncelleme uygulanamadı; önceki sürüme dönüldü.");
                System.Windows.MessageBox.Show(
                    "Güncelleme uygulanamadı ve önceki sürüme dönüldü.",
                    "Güncelleme",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            else
            {
                UpdateLog.Write("Taşınabilir güncelleme tamamlandı.");
            }
        }
        catch
        {
            // Sonuç dosyası okunamazsa güncelleme akışı etkilenmez.
        }
    }

    private async Task DownloadAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Geçici dosya silinemezse bir sonraki indirme üzerine yazar.
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CGP-RandevuTakip-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
