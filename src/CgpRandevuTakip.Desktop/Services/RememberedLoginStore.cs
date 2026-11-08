using System.IO;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

public interface IRememberedLoginStore
{
    string? Load();
    void Save(string token);
    void Clear();
}

/// <summary>
/// "Beni hatırla" token'ını DPAPI ile koruyarak yerel dosyada saklar.
/// Parola hiçbir zaman saklanmaz.
/// </summary>
public sealed class RememberedLoginStore : IRememberedLoginStore
{
    private readonly ISecretProtector _protector;
    private readonly string _path = Path.Combine(AppPaths.Root, "remember.dat");

    public RememberedLoginStore(ISecretProtector protector)
    {
        _protector = protector;
    }

    public string? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;

            var protectedToken = File.ReadAllText(_path).Trim();
            if (protectedToken.Length == 0)
                return null;

            var token = _protector.Unprotect(protectedToken);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    public void Save(string token)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, _protector.Protect(token) ?? string.Empty);
        }
        catch
        {
            // Hatırlama başarısız olsa bile giriş akışı devam eder.
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch
        {
            // Dosya silinemezse bir sonraki başarılı girişte üzerine yazılır.
        }
    }
}
