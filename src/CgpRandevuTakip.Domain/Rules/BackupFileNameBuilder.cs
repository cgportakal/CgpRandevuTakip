using System.Globalization;
using System.Text.RegularExpressions;
using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Rules;

/// <summary>
/// Yedek dosya adı: CGP_RandevuTakip_Yedek_yyyy-AA-gg_SS-dd-ss.zip
/// veya şifreli yedeklerde .cgpbak. Ad sıralanabilir ve okunaklıdır.
/// </summary>
public static partial class BackupFileNameBuilder
{
    public const string Prefix = AppConstants.ProductCode + "_Yedek_";
    public const string Extension = ".zip";
    public const string EncryptedExtension = ".cgpbak";

    public static string Build(DateTime localTimestamp, bool encrypted = false)
        => $"{Prefix}{localTimestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}" +
           (encrypted ? EncryptedExtension : Extension);

    public static bool IsValid(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) && FileNamePattern().IsMatch(fileName);

    public static bool IsEncrypted(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && fileName.EndsWith(EncryptedExtension, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^CGP_RandevuTakip_Yedek_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.(?:zip|cgpbak)$")]
    private static partial Regex FileNamePattern();
}
