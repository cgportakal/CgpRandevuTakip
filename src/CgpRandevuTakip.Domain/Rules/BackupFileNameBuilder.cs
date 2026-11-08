using System.Globalization;
using System.Text.RegularExpressions;
using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Rules;

/// <summary>
/// Yedek dosya adı: CGP_RandevuTakip_Yedek_yyyy-AA-gg_SS-dd-ss.zip
/// Windows'ta yasak karakterler kullanılmaz; ad sıralanabilir ve okunaklıdır.
/// </summary>
public static partial class BackupFileNameBuilder
{
    public const string Prefix = AppConstants.ProductCode + "_Yedek_";
    public const string Extension = ".zip";

    public static string Build(DateTime localTimestamp)
        => $"{Prefix}{localTimestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}{Extension}";

    public static bool IsValid(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) && FileNamePattern().IsMatch(fileName);

    [GeneratedRegex(@"^CGP_RandevuTakip_Yedek_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.zip$")]
    private static partial Regex FileNamePattern();
}
