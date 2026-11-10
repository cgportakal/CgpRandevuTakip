using System.Text.RegularExpressions;

namespace CgpRandevuTakip.Domain.Rules;

public enum BitLockerProtectionStatus
{
    Unknown = 0,
    Off = 1,
    On = 2
}

/// <summary>BitLocker koruma durumu çıktısını yorumlar.</summary>
public static partial class BitLockerStatusParser
{
    public static BitLockerProtectionStatus Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return BitLockerProtectionStatus.Unknown;

        if (OnPattern().IsMatch(output))
            return BitLockerProtectionStatus.On;

        if (OffPattern().IsMatch(output))
            return BitLockerProtectionStatus.Off;

        return BitLockerProtectionStatus.Unknown;
    }

    [GeneratedRegex(@"\bOn\b", RegexOptions.IgnoreCase)]
    private static partial Regex OnPattern();

    [GeneratedRegex(@"\bOff\b", RegexOptions.IgnoreCase)]
    private static partial Regex OffPattern();
}
