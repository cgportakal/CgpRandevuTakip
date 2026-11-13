using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

public sealed record UpdateSnapshot(string Version, DateTime CreatedAtUtc);

public sealed record PendingUpdate(
    string FromVersion,
    string TargetVersion,
    string AppDirectory,
    bool Portable,
    DateTime StartedAtUtc);

/// <summary>
/// Güncelleme öncesi program dosyalarının anlık görüntüsünü alır; kurulum
/// uygulanamazsa veya kullanıcı isterse önceki sürüme döner.
/// Veri klasörü (CGP-Data) hiçbir zaman kopyalanmaz/silinmez.
/// </summary>
public sealed class UpdateRollbackService
{
    private const string UpdatesFolderName = "updates";
    private const string RollbackFolderName = "rollback";
    private const string AppFolderName = "app";
    private const string VersionFileName = "version.txt";
    private const string PendingFileName = "pending.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory;

    public UpdateRollbackService()
        : this(AppPaths.Root)
    {
    }

    public UpdateRollbackService(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string UpdatesRoot => Path.Combine(_rootDirectory, UpdatesFolderName);
    public string SnapshotRoot => Path.Combine(UpdatesRoot, RollbackFolderName);
    public string SnapshotAppDirectory => Path.Combine(SnapshotRoot, AppFolderName);

    private string VersionFilePath => Path.Combine(SnapshotRoot, VersionFileName);
    private string PendingFilePath => Path.Combine(UpdatesRoot, PendingFileName);

    public UpdateSnapshot? GetSnapshot()
    {
        try
        {
            if (!Directory.Exists(SnapshotAppDirectory) || !File.Exists(VersionFilePath))
                return null;

            var version = File.ReadAllText(VersionFilePath).Trim();
            if (string.IsNullOrWhiteSpace(version))
                return null;

            return new UpdateSnapshot(
                version,
                Directory.GetLastWriteTimeUtc(SnapshotAppDirectory));
        }
        catch
        {
            return null;
        }
    }

    public bool CanRollback()
    {
        var snapshot = GetSnapshot();
        return snapshot is not null
               && !string.Equals(snapshot.Version, AppConstants.Version, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Güncelleme öncesi mevcut program dosyalarını saklar ve bekleyen güncelleme
    /// işaretini yazar. Hata durumunda güncelleme başlatılmamalıdır.
    /// </summary>
    public bool Prepare(string targetVersion, bool portable, string? sourceDirectory = null)
    {
        try
        {
            var appDirectory = (sourceDirectory ?? AppContext.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar);

            if (Directory.Exists(SnapshotRoot))
                Directory.Delete(SnapshotRoot, recursive: true);

            Directory.CreateDirectory(SnapshotAppDirectory);
            CopyDirectory(appDirectory, SnapshotAppDirectory, excludedDirectory: Path.Combine(appDirectory, "CGP-Data"));

            File.WriteAllText(VersionFilePath, AppConstants.Version);

            var pending = new PendingUpdate(
                AppConstants.Version,
                targetVersion,
                appDirectory,
                portable,
                DateTime.UtcNow);

            Directory.CreateDirectory(UpdatesRoot);
            File.WriteAllText(PendingFilePath, JsonSerializer.Serialize(pending, JsonOptions));

            UpdateLog.Write($"Geri dönüş anlık görüntüsü hazırlandı ({AppConstants.Version}).");
            return true;
        }
        catch (Exception ex)
        {
            UpdateLog.Write($"Geri dönüş anlık görüntüsü hazırlanamadı: {ex.Message}");
            return false;
        }
    }

    public PendingUpdate? ReadPending()
    {
        try
        {
            if (!File.Exists(PendingFilePath))
                return null;

            return JsonSerializer.Deserialize<PendingUpdate>(
                File.ReadAllText(PendingFilePath),
                JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void ClearPending()
    {
        try
        {
            if (File.Exists(PendingFilePath))
                File.Delete(PendingFilePath);
        }
        catch
        {
            // Bir sonraki açılışta yeniden denenir.
        }
    }

    /// <summary>
    /// Açılışta bekleyen güncellemeyi değerlendirir. Güncelleme uygulanmadıysa
    /// geri dönüş betiğini başlatır ve uygulamanın kapanması gerektiğini döndürür.
    /// </summary>
    public bool HandlePendingOnStartup()
    {
        var pending = ReadPending();
        if (pending is null)
            return false;

        if (!AppVersionComparer.IsNewer(pending.TargetVersion, AppConstants.Version))
        {
            UpdateLog.Write(
                $"Güncelleme tamamlandı: {pending.FromVersion} → {AppConstants.Version}.");
            ClearPending();
            return false;
        }

        UpdateLog.Write(
            $"Güncelleme uygulanamadı (hedef {pending.TargetVersion}, mevcut {AppConstants.Version}). Önceki sürüme dönülüyor.");

        if (GetSnapshot() is null)
        {
            UpdateLog.Write("Geri dönüş anlık görüntüsü bulunamadı; işlem atlandı.");
            ClearPending();
            MessageBox.Show(
                "Güncelleme uygulanamadı ve geri dönüş dosyaları bulunamadı.\n\nUygulama mevcut sürümle açılacak.",
                "Güncelleme",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        MessageBox.Show(
            "Güncelleme tamamlanamadı. Uygulama önceki sürüme döndürülecek ve yeniden başlatılacak.",
            "Güncelleme",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        StartRollbackScript(deletePending: true);
        return true;
    }

    /// <summary>Kullanıcı isteğiyle önceki sürüme döner.</summary>
    public bool StartManualRollback()
    {
        if (!CanRollback())
            return false;

        UpdateLog.Write(
            $"Kullanıcı önceki sürüme dönüyor: {AppConstants.Version} → {GetSnapshot()?.Version}.");

        StartRollbackScript(deletePending: false);
        return true;
    }

    private void StartRollbackScript(bool deletePending)
    {
        try
        {
            Directory.CreateDirectory(UpdatesRoot);

            var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var exePath = Path.Combine(appDirectory, "CgpRandevuTakip.Desktop.exe");
            var scriptPath = Path.Combine(UpdatesRoot, "rollback.cmd");
            var dataDirectory = Path.Combine(appDirectory, "CGP-Data");

            var pendingDelete = deletePending && File.Exists(PendingFilePath)
                ? $"del \"{PendingFilePath}\" >nul 2>&1"
                : string.Empty;

            var script = $"""
                @echo off
                timeout /t 3 /nobreak >nul
                :waitloop
                tasklist /fi "imagename eq CgpRandevuTakip.Desktop.exe" | find /i "CgpRandevuTakip.Desktop.exe" >nul
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >nul
                    goto waitloop
                )
                robocopy "{SnapshotAppDirectory}" "{appDirectory}" /E /R:3 /W:1 /NFL /NDL /NJH /NJS /XD "{dataDirectory}"
                {pendingDelete}
                start "" "{exePath}"
                del "%~f0"
                """;

            File.WriteAllText(scriptPath, script);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            UpdateLog.Write($"Geri dönüş betiği başlatılamadı: {ex.Message}");
            MessageBox.Show(
                $"Önceki sürüme dönülemedi: {ex.Message}",
                "Güncelleme",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void CopyDirectory(string source, string target, string excludedDirectory)
    {
        var excluded = Path.GetFullPath(excludedDirectory);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(directory);
            if (full.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(target, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            if (full.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
