namespace CgpRandevuTakip.Application.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return $"Şifre en az {MinLength} karakter olmalıdır.";

        if (password.Length > MaxLength)
            return $"Şifre en fazla {MaxLength} karakter olabilir.";

        if (!password.Any(char.IsLetter))
            return "Şifre en az bir harf içermelidir.";

        if (!password.Any(char.IsDigit))
            return "Şifre en az bir rakam içermelidir.";

        return null;
    }
}
