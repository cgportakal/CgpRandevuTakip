using System.IO;
using System.Text;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Güncelleme denetimi, indirme ve kurulum adımlarını yerel güncelleme günlüğüne yazar.
/// Kişisel veri içermez; yalnızca sürüm, dosya ve hata bilgisi tutar.
/// </summary>
public static class UpdateLog
{
    private const long MaxBytes = 1_000_000;
    private static readonly object Gate = new();

    public static string FilePath => Path.Combine(AppPaths.LogsDirectory, "update.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogsDirectory);

                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    var backup = FilePath + ".old";
                    File.Copy(FilePath, backup, overwrite: true);
                    File.WriteAllText(FilePath, string.Empty, Encoding.UTF8);
                }

                File.AppendAllText(
                    FilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Güncelleme günlüğü yazılamazsa güncelleme akışı durdurulmaz.
        }
    }

    public static string? ReadTail(int maxCharacters = 20000)
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var text = File.ReadAllText(FilePath);
            return text.Length <= maxCharacters
                ? text
                : text[^maxCharacters..];
        }
        catch
        {
            return null;
        }
    }
}
