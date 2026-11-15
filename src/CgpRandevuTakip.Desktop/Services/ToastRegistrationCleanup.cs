using System.IO;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Windows bildirim kayıtlarını temizler. Paketlenmemiş uygulamalarda
/// <c>ToastNotificationManagerCompat</c> her EXE yolu için
/// <c>HKCU\Software\Classes\AppUserModelId</c> anahtarı, bir COM sınıfı ve ikon klasörü oluşturur.
/// Taşınan veya silinen kopyalar geride ölü kayıt bırakır; bu servis yalnızca
/// hedefi artık var olmayan CGP kayıtlarını siler, diğer uygulamalara dokunmaz.
/// </summary>
public static class ToastRegistrationCleanup
{
    private const string AppUserModelIdRoot = @"Software\Classes\AppUserModelId";
    private const string ClsidRoot = @"Software\Classes\CLSID";
    private const string ExeName = "CgpRandevuTakip.Desktop.exe";

    public static int CleanupStale()
    {
        var removed = 0;

        try
        {
            removed += CleanupAppUserModelIds();
            removed += CleanupOrphanClsids();
        }
        catch
        {
            // Bildirim kayıtları temizlenemezse uygulama akışı etkilenmez.
        }

        return removed;
    }

    private static int CleanupAppUserModelIds()
    {
        var removed = 0;

        using var root = Registry.CurrentUser.OpenSubKey(AppUserModelIdRoot, writable: true);
        if (root is null)
            return 0;

        foreach (var name in root.GetSubKeyNames())
        {
            if (!name.Contains(ExeName, StringComparison.OrdinalIgnoreCase))
                continue;

            var target = NormalizePath(name);
            if (target is not null && File.Exists(target))
                continue;

            using var subKey = root.OpenSubKey(name);
            var activator = subKey?.GetValue("CustomActivator") as string;

            if (!string.IsNullOrWhiteSpace(activator))
                DeleteClsid(activator);

            try
            {
                root.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
                removed++;
            }
            catch
            {
                // Tek kayıt silinemezse diğerleri denenir.
            }
        }

        return removed;
    }

    private static int CleanupOrphanClsids()
    {
        var removed = 0;

        using var root = Registry.CurrentUser.OpenSubKey(ClsidRoot, writable: true);
        if (root is null)
            return 0;

        foreach (var name in root.GetSubKeyNames())
        {
            using var serverKey = root.OpenSubKey($@"{name}\LocalServer32");
            if (serverKey?.GetValue(null) is not string command)
                continue;

            if (!command.Contains(ExeName, StringComparison.OrdinalIgnoreCase))
                continue;

            var target = AutoStartService.ExtractExecutablePath(command);
            if (target is not null && File.Exists(target))
                continue;

            try
            {
                root.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
                DeleteIconDirectory(name);
                removed++;
            }
            catch
            {
                // Tek kayıt silinemezse diğerleri denenir.
            }
        }

        return removed;
    }

    private static void DeleteClsid(string activator)
    {
        var guid = activator.Trim().Trim('{', '}');
        if (guid.Length == 0)
            return;

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"{ClsidRoot}\{{{guid}}}",
                throwOnMissingSubKey: false);
        }
        catch
        {
            // COM kaydı silinemezse ikon yine de temizlenir.
        }

        DeleteIconDirectory(guid);
    }

    private static void DeleteIconDirectory(string guid)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ToastNotificationManagerCompat",
                "Apps");

            var path = Path.Combine(root, guid.Trim().Trim('{', '}').ToUpperInvariant());
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // İkon klasörü silinemezse sistemde kalır; işlevi yoktur.
        }
    }

    /// <summary>AUMID anahtar adı "C:/..." biçiminde olabilir; Windows yoluna çevirir.</summary>
    private static string? NormalizePath(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return null;

        return keyName.Replace('/', Path.DirectorySeparatorChar);
    }
}
