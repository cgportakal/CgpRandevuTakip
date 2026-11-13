using System.Security.Cryptography;

namespace CgpRandevuTakip.Application.Updates;

/// <summary>
/// İndirilen güncelleme paketlerinin SHA-256 doğrulaması.
/// GitHub Releases asset <c>digest</c> alanı ile karşılaştırılır.
/// </summary>
public static class UpdateIntegrity
{
    public const int Sha256HexLength = 64;

    public static string? NormalizeDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
            return null;

        var value = digest.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..];

        value = value.Trim().ToLowerInvariant();
        return IsSha256(value) ? value : null;
    }

    public static bool IsSha256(string? value)
    {
        if (value is null || value.Length != Sha256HexLength)
            return false;

        foreach (var ch in value)
        {
            var isHex = ch is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
                return false;
        }

        return true;
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static bool Matches(string filePath, string? expectedDigest)
    {
        var expected = NormalizeDigest(expectedDigest);
        if (expected is null || !File.Exists(filePath))
            return false;

        return string.Equals(ComputeSha256(filePath), expected, StringComparison.Ordinal);
    }
}
