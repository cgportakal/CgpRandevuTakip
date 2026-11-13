using System.IO;
using CgpRandevuTakip.Infrastructure.Persistence;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>Elle kilit durumunu uygulama yeniden başlasa da korur.</summary>
public sealed class SessionLockStore
{
    private readonly string _path = Path.Combine(AppPaths.Root, "session.lock");

    public string? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;

            var value = File.ReadAllText(_path).Trim();
            return value.Length == 0 ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public bool IsLocked(string? userId)
        => !string.IsNullOrWhiteSpace(userId)
           && string.Equals(Load(), userId, StringComparison.Ordinal);

    public void SetLocked(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, userId);
        }
        catch
        {
            // Kilit durumu yazılamazsa uygulama yine kilitlenir; yalnızca yeniden başlatmada hatırlanmaz.
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
            // Kilit dosyası silinemezse bir sonraki başarılı açılışta ele alınır.
        }
    }
}
