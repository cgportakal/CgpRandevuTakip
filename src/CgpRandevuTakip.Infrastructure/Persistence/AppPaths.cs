namespace CgpRandevuTakip.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın yerel dosya yolları. Normal kurulumda kullanıcı profilinde,
/// taşınabilir modda EXE'nin yanındaki <c>CGP-Data</c> klasöründe tutulur.
/// </summary>
public static class AppPaths
{
    // Yalnızca 0.4.0 ve öncesindeki kurulumların verisini yeni klasöre taşımak için kullanılır.
    private const string LegacyRootFolderName = "BekRandevuTakip";
    public const string PortableFlagFileName = "portable.flag";
    public const string PortableDataFolderName = "CGP-Data";

    private static string? _root;

    public static bool IsPortable { get; private set; }

    public static string Root => _root ??= GetLocalAppDataRoot();

    public static string DatabasePath => Path.Combine(Root, "data.db");
    public static string LogsDirectory => Path.Combine(Root, "logs");
    public static string TempDirectory => Path.Combine(Root, "temp");

    public static string DefaultBackupDirectory => IsPortable
        ? Path.Combine(Root, "Yedekler")
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "CgpRandevuTakip",
            "Yedekler");

    /// <summary>
    /// Uygulama başlangıcında, diğer tüm yol erişimlerinden önce çağrılmalıdır.
    /// <c>--portable</c> argümanı veya EXE yanında <c>portable.flag</c> varsa taşınabilir mod açılır.
    /// Klasör yazılamıyorsa güvenli biçimde kullanıcı profiline döner.
    /// </summary>
    public static void Initialize(string[]? args = null)
    {
        var requested = args?.Any(x => x.Equals("--portable", StringComparison.OrdinalIgnoreCase)) == true
                        || File.Exists(Path.Combine(AppContext.BaseDirectory, PortableFlagFileName));

        if (requested)
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, PortableDataFolderName);
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, $".cgp-write-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "test");
                File.Delete(probe);

                _root = candidate;
                IsPortable = true;
                return;
            }
            catch
            {
                // EXE salt okunur bir konumdaysa taşınabilir moda geçilemez.
            }
        }

        _root = GetLocalAppDataRoot();
        IsPortable = false;
    }

    public static void EnsureCreated()
    {
        if (!IsPortable)
            MigrateLegacyRoot();

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(DefaultBackupDirectory);
    }

    private static string GetLocalAppDataRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CgpRandevuTakip");

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
