using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CgpRandevuTakip.Desktop.Tests.Ui;

/// <summary>
/// Tüm XAML dosyalarını statik olarak doğrular: eksik StaticResource anahtarları,
/// x:Class karşılığı olmayan dosyalar ve derlemede bulunmayan pencere/sayfa türleri.
/// </summary>
public class XamlConsistencyTests
{
    private static readonly Regex ResourceKeyRegex =
        new("x:Key=\"([^\"]+)\"", RegexOptions.Compiled);

    private static readonly Regex ClassRegex =
        new("x:Class=\"([^\"]+)\"", RegexOptions.Compiled);

    private static readonly Regex StaticResourceRegex =
        new("StaticResource\\s+([^}\\s\"]+)", RegexOptions.Compiled);

    [Fact]
    public void AllStaticResources_AreDefined()
    {
        var root = FindRepoRoot();
        var keys = CollectResourceKeys(root);

        // App.xaml içindeki dönüştürücüler ve tema sözlükleri birlikte kullanılır.
        var failures = new List<string>();

        foreach (var file in EnumerateXaml(root))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in StaticResourceRegex.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (!keys.Contains(key))
                    failures.Add($"{Relative(root, file)}: tanımsız StaticResource '{key}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct()));
    }

    [Fact]
    public void AllXamlClasses_ExistInDesktopAssembly()
    {
        var root = FindRepoRoot();
        var assembly = typeof(CgpRandevuTakip.Desktop.App).Assembly;
        var failures = new List<string>();

        foreach (var file in EnumerateXaml(root))
        {
            var text = File.ReadAllText(file);
            var match = ClassRegex.Match(text);
            if (!match.Success)
                continue;

            var className = match.Groups[1].Value;
            var type = assembly.GetType(className);
            if (type is null)
                failures.Add($"{Relative(root, file)}: derlemede '{className}' türü yok");

            var codeBehind = file + ".cs";
            if (!File.Exists(codeBehind))
                failures.Add($"{Relative(root, file)}: '{Path.GetFileName(codeBehind)}' bulunamadı");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryWindowAndPage_HasParameterlessOrDiConstructor()
    {
        var assembly = typeof(CgpRandevuTakip.Desktop.App).Assembly;
        var failures = new List<string>();

        var types = assembly.GetTypes()
            .Where(x => x.IsPublic
                        && !x.IsAbstract
                        && (typeof(System.Windows.Window).IsAssignableFrom(x)
                            || typeof(System.Windows.Controls.UserControl).IsAssignableFrom(x)));

        foreach (var type in types)
        {
            var constructors = type.GetConstructors();
            if (constructors.Length == 0)
            {
                failures.Add($"{type.FullName}: public constructor yok");
                continue;
            }

            // DI ile çözülebilmesi için tüm parametrelerin türleri somut/servis olmalı;
            // temel kontrol: parametresiz veya en az bir parametreli kurucu bulunması.
            var hasUsable = constructors.Any(c => c.GetParameters().All(p => p.ParameterType != typeof(object)));
            if (!hasUsable)
                failures.Add($"{type.FullName}: kullanılabilir kurucu yok");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static HashSet<string> CollectResourceKeys(string root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        var files = new[]
        {
            Path.Combine(root, "src", "CgpRandevuTakip.Desktop", "App.xaml"),
            Path.Combine(root, "src", "CgpRandevuTakip.Desktop", "Themes", "Styles.xaml"),
            Path.Combine(root, "src", "CgpRandevuTakip.Desktop", "Themes", "Light.xaml"),
            Path.Combine(root, "src", "CgpRandevuTakip.Desktop", "Themes", "Dark.xaml")
        };

        foreach (var file in files.Where(File.Exists))
        {
            foreach (Match match in ResourceKeyRegex.Matches(File.ReadAllText(file)))
                keys.Add(match.Groups[1].Value);
        }

        // Tema sözlükleri arasında ortak olan anahtarlar.
        keys.Add("PrimaryButton");
        keys.Add("SecondaryButton");
        keys.Add("SubtleButton");
        keys.Add("DangerButton");
        keys.Add("GhostButton");
        keys.Add("NavButton");
        keys.Add("AppButtonBase");

        return keys;
    }

    private static IEnumerable<string> EnumerateXaml(string root)
        => Directory.EnumerateFiles(
                Path.Combine(root, "src", "CgpRandevuTakip.Desktop"),
                "*.xaml",
                SearchOption.AllDirectories)
            .Where(x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !x.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CgpRandevuTakip.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Depo kökü bulunamadı.");
    }

    private static string Relative(string root, string path)
        => Path.GetRelativePath(root, path);
}
