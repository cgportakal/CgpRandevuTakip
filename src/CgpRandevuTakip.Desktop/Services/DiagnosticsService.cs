using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Destek için kişisel veri içermeyen tanılama paketi üretir ve eski günlükleri temizler.
/// </summary>
public static class DiagnosticsService
{
    public static string CreateBundle()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var bundlePath = Path.Combine(desktop, $"CGP-Tanilama-{stamp}.zip");

        var tempRoot = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", $"diag-{stamp}");
        var logsTarget = Path.Combine(tempRoot, "logs");
        Directory.CreateDirectory(logsTarget);

        try
        {
            if (Directory.Exists(AppPaths.LogsDirectory))
            {
                foreach (var log in Directory.EnumerateFiles(AppPaths.LogsDirectory, "*.log", SearchOption.TopDirectoryOnly))
                {
                    var target = Path.Combine(logsTarget, Path.GetFileName(log));
                    CopyTail(log, target, maxBytes: 2_000_000);
                }
            }

            File.WriteAllText(
                Path.Combine(tempRoot, "system-info.txt"),
                BuildSystemInfo(),
                Encoding.UTF8);

            if (File.Exists(bundlePath))
                File.Delete(bundlePath);

            ZipFile.CreateFromDirectory(tempRoot, bundlePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return bundlePath;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Geçici klasör silinemezse sistem temizler.
            }
        }
    }

    public static void RevealInExplorer(string filePath)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
        {
            UseShellExecute = true
        });
    }

    /// <summary>90 günden eski günlük dosyalarını siler.</summary>
    public static int CleanupOldLogs(int retentionDays = 90)
    {
        if (!Directory.Exists(AppPaths.LogsDirectory))
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var removed = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.LogsDirectory, "*.log", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) >= cutoff)
                        continue;

                    File.Delete(file);
                    removed++;
                }
                catch
                {
                    // Tek dosya silinemezse diğerleri denenir.
                }
            }
        }
        catch
        {
            // Günlük klasörü okunamazsa temizlik atlanır.
        }

        return removed;
    }

    private static string BuildSystemInfo()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Sürüm: {AppConstants.Version}");
        builder.AppendLine($"Şema: {AppConstants.SchemaVersion}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($"64-bit süreç: {Environment.Is64BitProcess}");
        builder.AppendLine($"Taşınabilir: {AppPaths.IsPortable}");
        builder.AppendLine($"Veri klasörü: {AppPaths.Root}");
        builder.AppendLine($"Veritabanı: {AppPaths.DatabasePath}");
        builder.AppendLine($"Günlükler: {AppPaths.LogsDirectory}");
        builder.AppendLine($"Toplanma: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        try
        {
            var db = new FileInfo(AppPaths.DatabasePath);
            builder.AppendLine($"Veritabanı boyutu: {(db.Exists ? db.Length : 0)} bayt");
        }
        catch
        {
            builder.AppendLine("Veritabanı boyutu: okunamadı");
        }

        return builder.ToString();
    }

    private static void CopyTail(string source, string target, int maxBytes)
    {
        try
        {
            var info = new FileInfo(source);
            if (!info.Exists)
                return;

            if (info.Length <= maxBytes)
            {
                File.Copy(source, target, overwrite: true);
                return;
            }

            using var input = info.OpenRead();
            input.Seek(-maxBytes, SeekOrigin.End);
            using var output = File.Create(target);
            input.CopyTo(output);
        }
        catch
        {
            // Tek günlük kopyalanamazsa paket diğerleriyle oluşturulur.
        }
    }
}
