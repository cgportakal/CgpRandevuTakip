using System.Security.Cryptography;
using System.Text;

namespace CgpRandevuTakip.Infrastructure.Backups;

/// <summary>
/// Yedek ZIP'ini AES-256-GCM ile şifreler. Anahtar PBKDF2-SHA256 ile paroladan üretilir.
/// Dosya biçimi: başlık (sürüm, salt, iterasyon, nonce) + GCM etiketi + şifreli içerik.
/// </summary>
public static class BackupCipher
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CGPBAK1");
    private const byte FormatVersion = 1;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int DefaultIterations = 210_000;

    public static bool IsEncryptedFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[Magic.Length];
            if (stream.Read(header, 0, header.Length) != header.Length)
                return false;

            return header.AsSpan().SequenceEqual(Magic);
        }
        catch
        {
            return false;
        }
    }

    public static void EncryptFile(string sourcePath, string destinationPath, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var plain = File.ReadAllBytes(sourcePath);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt, DefaultIterations);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, cipher, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        using var output = File.Create(destinationPath);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(salt);
        writer.Write(DefaultIterations);
        writer.Write(nonce);
        writer.Write(tag);
        writer.Write((long)cipher.Length);
        writer.Write(cipher);
    }

    public static void DecryptFile(string sourcePath, string destinationPath, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using var input = File.OpenRead(sourcePath);
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);

        var header = reader.ReadBytes(Magic.Length);
        if (header.Length != Magic.Length || !header.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Dosya şifreli bir CGP yedeği değil.");

        var version = reader.ReadByte();
        if (version != FormatVersion)
            throw new InvalidDataException($"Desteklenmeyen yedek biçimi: {version}.");

        var salt = reader.ReadBytes(SaltSize);
        var iterations = reader.ReadInt32();
        if (iterations < 10_000 || iterations > 2_000_000)
            throw new InvalidDataException("Geçersiz anahtar türetme ayarı.");

        var nonce = reader.ReadBytes(NonceSize);
        var tag = reader.ReadBytes(TagSize);
        var length = reader.ReadInt64();
        if (length < 0 || length > 2L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Geçersiz şifreli içerik uzunluğu.");

        var cipher = new byte[length];
        var read = 0;
        while (read < cipher.Length)
        {
            var chunk = reader.Read(cipher, read, cipher.Length - read);
            if (chunk == 0)
                throw new InvalidDataException("Şifreli yedek dosyası eksik.");
            read += chunk;
        }

        var key = DeriveKey(password, salt, iterations);
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("Parola yanlış veya şifreli yedek bozuk.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        File.WriteAllBytes(destinationPath, plain);
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize);
}
