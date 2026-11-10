using CgpRandevuTakip.Infrastructure.Backups;

namespace CgpRandevuTakip.Tests.Infrastructure;

public class BackupCipherTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "CgpCipherTests",
        Guid.NewGuid().ToString("N"));

    public BackupCipherTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var source = Path.Combine(_root, "source.zip");
        var encrypted = Path.Combine(_root, "backup.cgpbak");
        var decrypted = Path.Combine(_root, "decrypted.zip");
        var payload = new byte[4096];
        Random.Shared.NextBytes(payload);
        File.WriteAllBytes(source, payload);

        BackupCipher.EncryptFile(source, encrypted, "parola-123456");

        Assert.True(BackupCipher.IsEncryptedFile(encrypted));
        Assert.True(new FileInfo(encrypted).Length > payload.Length);

        BackupCipher.DecryptFile(encrypted, decrypted, "parola-123456");

        Assert.Equal(payload, File.ReadAllBytes(decrypted));
    }

    [Fact]
    public void Decrypt_WrongPasswordThrows()
    {
        var source = Path.Combine(_root, "source.zip");
        var encrypted = Path.Combine(_root, "backup.cgpbak");
        File.WriteAllText(source, "test verisi");
        BackupCipher.EncryptFile(source, encrypted, "dogru-parola");

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupCipher.DecryptFile(encrypted, Path.Combine(_root, "out.zip"), "yanlis-parola"));

        Assert.Contains("Parola", error.Message);
    }

    [Fact]
    public void Decrypt_TamperedFileThrows()
    {
        var source = Path.Combine(_root, "source.zip");
        var encrypted = Path.Combine(_root, "backup.cgpbak");
        File.WriteAllText(source, "test verisi");
        BackupCipher.EncryptFile(source, encrypted, "parola-123456");

        var bytes = File.ReadAllBytes(encrypted);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(encrypted, bytes);

        Assert.Throws<InvalidDataException>(() =>
            BackupCipher.DecryptFile(encrypted, Path.Combine(_root, "out.zip"), "parola-123456"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonuç korunur.
        }
    }
}
