namespace CgpRandevuTakip.Domain.ValueObjects;

/// <summary>
/// Telefon numarasını E.164 biçimine normalize eder.
/// Türkiye varsayılan ülke kodu +90'dır.
/// </summary>
public static class PhoneNumber
{
    public const string DefaultCountryCode = "90";

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return false;

        var hadPlus = trimmed.StartsWith('+');
        var hadInternationalPrefix = digits.StartsWith("00", StringComparison.Ordinal);
        if (hadInternationalPrefix)
            digits = digits[2..];

        if (!hadPlus && !hadInternationalPrefix)
        {
            if (digits.StartsWith('0') && digits.Length == 11)
                digits = DefaultCountryCode + digits[1..];
            else if (digits.Length == 10)
                digits = DefaultCountryCode + digits;
        }

        if (digits.Length is < 8 or > 15)
            return false;

        normalized = "+" + digits;
        return true;
    }

    public static string Normalize(string input)
        => TryNormalize(input, out var normalized)
            ? normalized
            : throw new ArgumentException("Geçersiz telefon numarası.", nameof(input));

    public static string ToWhatsAppDigits(string e164)
        => e164.TrimStart('+');
}
