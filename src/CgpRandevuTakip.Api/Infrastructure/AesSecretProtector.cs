using System.Security.Cryptography;
using System.Text;
using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Api.Infrastructure;

/// <summary>
/// Sunucuda saklanan sırları (SMTP parolası, push token) AES-GCM ile şifreler.
/// Anahtar <c>CGP_API_SECRET_KEY</c> (base64, 32 bayt) ile verilir; yoksa
/// veri klasöründe üretilip saklanır. Windows DPAPI'ye bağımlı değildir.
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesSecretProtector(ApiEnvironment environment)
    {
        var configured = Environment.GetEnvironmentVariable("CGP_API_SECRET_KEY");
        _key = !string.IsNullOrWhiteSpace(configured)
            ? Convert.FromBase64String(configured)
            : LoadOrCreateKey(Path.Combine(environment.RootDirectory, "secret.key"));

        if (_key.Length != 32)
            throw new InvalidOperationException("CGP_API_SECRET_KEY 32 bayt (base64) olmalıdır.");
    }

    public string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);

        return "v1:" + Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        if (!cipherText.StartsWith("v1:", StringComparison.Ordinal))
            return cipherText;

        try
        {
            var payload = Convert.FromBase64String(cipherText[3..]);
            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(NonceSize, TagSize);
            var cipher = payload.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        if (File.Exists(path))
        {
            var existing = Convert.FromBase64String(File.ReadAllText(path).Trim());
            if (existing.Length == 32)
                return existing;
        }

        var key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Convert.ToBase64String(key));
        return key;
    }
}
