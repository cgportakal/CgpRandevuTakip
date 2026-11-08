using System.Security.Cryptography;
using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Infrastructure.Security;

/// <summary>PBKDF2-SHA256 ile parola özetleme. Düz parola hiçbir yerde saklanmaz.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public PasswordHashResult Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return new PasswordHashResult(
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            DefaultIterations);
    }

    public bool Verify(string password, string hash, string salt, int iterations)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            return false;

        var expected = Convert.FromBase64String(hash);
        var saltBytes = Convert.FromBase64String(salt);

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            iterations <= 0 ? DefaultIterations : iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
