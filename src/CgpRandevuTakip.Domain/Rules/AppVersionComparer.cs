using System.Globalization;

namespace CgpRandevuTakip.Domain.Rules;

/// <summary>Sürüm etiketlerini (v1.2.3) karşılaştırır.</summary>
public static class AppVersionComparer
{
    public static bool IsNewer(string? candidate, string current)
    {
        var left = Parse(candidate);
        var right = Parse(current);
        return left is not null && right is not null && left > right;
    }

    public static Version? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash > 0)
            text = text[..dash];

        return Version.TryParse(text, out var version) ? version : null;
    }
}
