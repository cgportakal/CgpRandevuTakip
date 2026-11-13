using System.IO;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

public sealed record LogoImportResult(bool Success, string? Error, string? RelativePath);

/// <summary>
/// Kurum logosunu veri klasöründe saklar ve günlük liste görseli için çözer.
/// Ayarlarda mutlak yol değil, veri köküne göreli yol tutulur.
/// </summary>
public sealed class InstitutionLogoService
{
    public const long MaxFileSizeBytes = 4 * 1024 * 1024;
    private const string BrandingFolder = "branding";
    private const string BaseFileName = "institution-logo";

    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

    private readonly string _rootDirectory;

    public InstitutionLogoService()
        : this(AppPaths.Root)
    {
    }

    public InstitutionLogoService(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string BrandingDirectory => Path.Combine(_rootDirectory, BrandingFolder);

    public static string SupportedFormatsText => "PNG, JPG veya BMP · en fazla 4 MB";

    public string? ResolvePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        try
        {
            var path = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(
                    _rootDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    public BitmapImage? LoadPreview(string? relativePath)
    {
        var path = ResolvePath(relativePath);
        if (path is null)
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelHeight = 160;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public LogoImportResult Import(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return new LogoImportResult(false, "Logo dosyası bulunamadı.", null);

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return new LogoImportResult(false, $"Desteklenmeyen dosya türü. {SupportedFormatsText}", null);

        long size;
        try
        {
            size = new FileInfo(sourcePath).Length;
        }
        catch (Exception ex)
        {
            return new LogoImportResult(false, $"Logo okunamadı: {ex.Message}", null);
        }

        if (size == 0)
            return new LogoImportResult(false, "Logo dosyası boş.", null);

        if (size > MaxFileSizeBytes)
            return new LogoImportResult(false, $"Logo dosyası çok büyük. {SupportedFormatsText}", null);

        try
        {
            Directory.CreateDirectory(BrandingDirectory);
            var fileName = $"{BaseFileName}{extension}";
            var targetPath = Path.Combine(BrandingDirectory, fileName);

            foreach (var existing in Directory.EnumerateFiles(
                         BrandingDirectory,
                         $"{BaseFileName}.*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(existing, targetPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(existing);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);

            return new LogoImportResult(
                true,
                null,
                $"{BrandingFolder}/{fileName}");
        }
        catch (Exception ex)
        {
            return new LogoImportResult(false, $"Logo kaydedilemedi: {ex.Message}", null);
        }
    }

    public void Remove()
    {
        try
        {
            if (!Directory.Exists(BrandingDirectory))
                return;

            foreach (var existing in Directory.EnumerateFiles(
                         BrandingDirectory,
                         $"{BaseFileName}.*",
                         SearchOption.TopDirectoryOnly))
            {
                File.Delete(existing);
            }
        }
        catch
        {
            // Logo silinemezse ayar temizlenir; dosya bir sonraki seçimde değiştirilir.
        }
    }
}
