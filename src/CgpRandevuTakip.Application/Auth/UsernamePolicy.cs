namespace CgpRandevuTakip.Application.Auth;

/// <summary>Kullanıcı adı normalizasyonu ve doğrulaması; giriş ve host yönetiminde ortaktır.</summary>
public static class UsernamePolicy
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    public static string Normalize(string? username)
        => (username ?? string.Empty).Trim().ToLowerInvariant();

    public static string? Validate(string username)
    {
        if (username.Length < MinLength)
            return $"Kullanıcı adı en az {MinLength} karakter olmalıdır.";

        if (username.Length > MaxLength)
            return $"Kullanıcı adı en fazla {MaxLength} karakter olabilir.";

        if (!username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            return "Kullanıcı adı yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.";

        return null;
    }
}
