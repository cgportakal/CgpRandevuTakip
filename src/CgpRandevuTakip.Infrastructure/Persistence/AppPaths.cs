namespace CgpRandevuTakip.Infrastructure.Persistence;

/// <summary>Uygulamanın yerel dosya yolları. Tüm veri kullanıcı profilinde tutulur.</summary>
public static class AppPaths
{
    // Yalnızca 0.4.0 ve öncesindeki kurulumların verisini yeni klasöre taşımak için kullanılır.
    private const string LegacyRootFolderName = "BekRandevuTakip";

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CgpRandevuTakip");

    public static string DatabasePath => Path.Combine(Root, "data.db");
    public static string LogsDirectory => Path.Combine(Root, "logs");
    public static string TempDirectory => Path.Combine(Path.GetTempPath(), "CgpRandevuTakip");

    public static string DefaultBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CgpRandevuTakip",
        "Yedekler");

    public static void EnsureCreated()
    {
        MigrateLegacyRoot();
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(DefaultBackupDirectory);
    }

    private static void MigrateLegacyRoot()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var legacyRoot = Path.Combine(localAppData, LegacyRootFolderName);

            if (!Directory.Exists(Root) && Directory.Exists(legacyRoot))
                Directory.Move(legacyRoot, Root);
        }
        catch
        {
            // Taşıma başarısız olursa yeni klasör kullanılır; eski veri yerinde kalır.
        }
    }
}
