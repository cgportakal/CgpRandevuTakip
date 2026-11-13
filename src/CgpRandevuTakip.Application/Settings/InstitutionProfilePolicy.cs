using CgpRandevuTakip.Application.Users;

namespace CgpRandevuTakip.Application.Settings;

/// <summary>
/// Kurum profili alanlarının ortak doğrulaması. Hem kurulum sihirbazı hem
/// ayarlar ekranı aynı kuralları kullanır.
/// </summary>
public static class InstitutionProfilePolicy
{
    public const int MaxNameLength = 120;
    public const int MaxAddressLength = 250;

    public static string? ValidateName(string? value, bool required)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return required ? "Kurum adı zorunludur." : null;

        return name.Length > MaxNameLength
            ? $"Kurum adı en fazla {MaxNameLength} karakter olabilir."
            : null;
    }

    public static string? ValidateEmail(string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
            return required ? "Kurum e-posta adresi zorunludur." : null;

        return EmailAddressPolicy.TryNormalize(value, out _)
            ? null
            : "Kurum e-posta adresi geçerli değil.";
    }

    public static string? ValidatePhone(string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
            return required ? "Kurum telefonu zorunludur." : null;

        if (value.Trim().Length > 25 || !TryNormalizePhone(value, out _))
            return "Telefon en az 10, en fazla 13 rakam içermelidir. Örn: 0532 123 45 67";

        return null;
    }

    public static string? ValidateAddress(string? value, bool required)
    {
        var address = value?.Trim() ?? string.Empty;
        if (address.Length == 0)
            return required ? "Kurum adresi zorunludur." : null;

        if (address.Length < 10)
            return "Adres en az 10 karakter olmalıdır.";

        return address.Length > MaxAddressLength
            ? $"Adres en fazla {MaxAddressLength} karakter olabilir."
            : null;
    }

    public static bool TryNormalizePhone(string? value, out string phone)
    {
        phone = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length > 25)
            return false;

        foreach (var ch in trimmed)
        {
            if (!char.IsDigit(ch) && ch is not (' ' or '-' or '(' or ')' or '+' or '.' or '/'))
                return false;
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 13)
            return false;

        if (digits.Distinct().Count() == 1)
            return false;

        phone = trimmed;
        return true;
    }
}
