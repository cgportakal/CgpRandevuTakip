using System.IO;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

public interface IRememberedLoginStore
{
    string? Load();
    void Save(string token);
    void Clear();

    (string Username, string Password)? LoadCredentials();
    void SaveCredentials(string username, string password);
    void ClearCredentials();
}

/// <summary>
/// "Beni hatırla" token'ını DPAPI ile koruyarak yerel dosyada saklar.
/// Parola hiçbir zaman saklanmaz.
/// </summary>
public sealed class RememberedLoginStore : IRememberedLoginStore
{
    private readonly ISecretProtector _protector;
    private readonly string _path = Path.Combine(AppPaths.Root, "remember.dat");
    private readonly string _credentialsPath = Path.Combine(AppPaths.Root, "remember.cred");

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
        ClearFile(_path);
        ClearFile(_credentialsPath);
    }

    public (string Username, string Password)? LoadCredentials()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
                return null;

            var lines = File.ReadAllLines(_credentialsPath);
            if (lines.Length < 2 || string.IsNullOrWhiteSpace(lines[0]))
                return null;

            var password = _protector.Unprotect(lines[1]) ?? string.Empty;
            return (lines[0].Trim(), password);
        }
        catch
        {
            return null;
        }
    }

    public void SaveCredentials(string username, string password)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_credentialsPath)!);
            var protectedPassword = _protector.Protect(password) ?? string.Empty;
            File.WriteAllLines(_credentialsPath, new[] { username, protectedPassword });
        }
        catch
        {
            // Hatırlama başarısız olsa bile giriş akışı devam eder.
        }
    }

    public void ClearCredentials() => ClearFile(_credentialsPath);

    private static void ClearFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Dosya silinemezse bir sonraki başarılı girişte üzerine yazılır.
        }
    }
}
