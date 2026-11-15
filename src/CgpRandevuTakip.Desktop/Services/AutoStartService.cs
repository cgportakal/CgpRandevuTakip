using System.IO;
using System.Text;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Windows oturumu açıldığında uygulamayı gizli (--tray) başlatır.
/// Kurulu sürümde HKCU Run, taşınabilir sürümde Başlangıç klasöründe VBS başlatıcı kullanılır.
/// VBS, hedef EXE yoksa sessizce çıkar; böylece taşınan/silinen kopyalar Windows Script Host
/// hata penceresi göstermez.
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CgpRandevuTakip";
    private const string StartupFileName = "CGP - Randevu Takip.vbs";
    private const string LegacyShortcutName = "CGP - Randevu Takip.lnk";

    private readonly string _startupDirectory;
    private readonly string _runKeyPath;
    private readonly string _valueName;
    private readonly Func<string?> _exePathProvider;
    private readonly Func<bool> _isPortable;

    public AutoStartService()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            RunKeyPath,
            ValueName,
            () => Environment.ProcessPath,
            () => AppPaths.IsPortable)
    {
    }

    /// <summary>Testler için yolları ve modu dışarıdan alan kurucu.</summary>
    public AutoStartService(
        string startupDirectory,
        string runKeyPath,
        string valueName,
        Func<string?> exePathProvider,
        Func<bool> isPortable)
    {
        _startupDirectory = startupDirectory;
        _runKeyPath = runKeyPath;
        _valueName = valueName;
        _exePathProvider = exePathProvider;
        _isPortable = isPortable;
    }

    private string StartupFilePath => Path.Combine(_startupDirectory, StartupFileName);

    private string LegacyShortcutPath => Path.Combine(_startupDirectory, LegacyShortcutName);

    public bool IsEnabled()
    {
        if (_isPortable())
            return File.Exists(StartupFilePath);

        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        return key?.GetValue(_valueName) is string value && value.Length > 0;
    }

    public void SetEnabled(bool enabled, string startMode)
    {
        try
        {
            if (_isPortable())
                SetPortableEnabled(enabled);
            else
                SetInstalledEnabled(enabled, startMode);
        }
        catch
        {
            // Otomatik başlatma yazılamazsa uygulama normal açılır.
        }
    }

    /// <summary>
    /// Açılışta çağrılır: hedefi silinmiş başlatıcıları temizler ve geçerli kipi onarır.
    /// Böylece eski portable kopyalardan kalan ölü VBS bir daha hata penceresi üretmez.
    /// </summary>
    public void Repair(bool enabled, string startMode)
    {
        RemoveInvalidRunValue();
        RemoveInvalidStartupLauncher();
        SetEnabled(enabled, startMode);
    }

    private void SetInstalledEnabled(bool enabled, string startMode)
    {
        if (!enabled)
        {
            RemoveRunValue();
            return;
        }

        var exePath = _exePathProvider();
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        var arguments = startMode == "tray" ? "--tray" : string.Empty;
        var command = string.IsNullOrWhiteSpace(arguments)
            ? exePath
            : $"\"{exePath}\" {arguments}";

        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath);
        key.SetValue(_valueName, command, RegistryValueKind.String);

        // Kurulu sürüm başlangıçta kendi kaydını yazdığında, geçerli olsa bile
        // portable başlatıcı kalmaz: tek bir otomatik başlatma girdisi olur.
        DeleteStartupLaunchers();
    }

    private void SetPortableEnabled(bool enabled)
    {
        if (!enabled)
        {
            DeleteStartupLaunchers();
            return;
        }

        var exePath = _exePathProvider();
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        Directory.CreateDirectory(_startupDirectory);
        File.WriteAllText(StartupFilePath, BuildPortableScript(exePath), new UnicodeEncoding(false, true));

        if (File.Exists(LegacyShortcutPath))
            File.Delete(LegacyShortcutPath);
    }

    /// <summary>
    /// Başlangıç klasörüne yazılan VBS. Hedef yoksa veya herhangi bir hata olursa
    /// yol açmadan çıkar; Windows Script Host penceresi göstermez.
    /// </summary>
    public static string BuildPortableScript(string exePath)
    {
        var escaped = exePath.Replace("\"", "\"\"", StringComparison.Ordinal);

        return string.Join(
            "\r\n",
            "On Error Resume Next",
            "Dim sh, fso, exePath",
            $"exePath = \"{escaped}\"",
            "Set fso = CreateObject(\"Scripting.FileSystemObject\")",
            "If fso.FileExists(exePath) Then",
            "    Set sh = CreateObject(\"WScript.Shell\")",
            "    sh.Run \"\"\"\" & exePath & \"\"\" --tray\", 0, False",
            "End If",
            string.Empty);
    }

    private void DeleteStartupLaunchers()
    {
        try
        {
            if (File.Exists(StartupFilePath))
                File.Delete(StartupFilePath);

            if (File.Exists(LegacyShortcutPath))
                File.Delete(LegacyShortcutPath);
        }
        catch
        {
            // Dosya kilitliyse bir sonraki denemede ele alınır.
        }
    }

    private void RemoveRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
            if (key?.GetValue(_valueName) is not null)
                key.DeleteValue(_valueName, throwOnMissingValue: false);
        }
        catch
        {
            // Kayıt defteri yazılamıyorsa ayar bir sonraki açılışta yeniden denenir.
        }
    }

    private void RemoveInvalidRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            if (key?.GetValue(_valueName) is not string value)
                return;

            if (IsValidLauncherCommand(value))
                return;

            using var writable = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
            writable?.DeleteValue(_valueName, throwOnMissingValue: false);
        }
        catch
        {
            // Okunamayan değer silinmez.
        }
    }

    private void RemoveInvalidStartupLauncher()
    {
        try
        {
            if (!File.Exists(StartupFilePath))
                return;

            var target = TryReadLauncherTarget(StartupFilePath);
            if (target is not null && File.Exists(target))
                return;

            File.Delete(StartupFilePath);
        }
        catch
        {
            // Dosya silinemezse bir sonraki açılışta yeniden denenir.
        }
    }

    private static bool IsValidLauncherCommand(string command)
    {
        var exePath = ExtractExecutablePath(command);
        return exePath is not null && File.Exists(exePath);
    }

    /// <summary>Run değerinden veya VBS içinden EXE yolunu çıkarır.</summary>
    public static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var text = command.Trim();

        // VBS biçimi: exePath = "C:\...\uygulama.exe"
        var assignment = text.IndexOf("exePath", StringComparison.OrdinalIgnoreCase);
        if (assignment >= 0)
        {
            var start = text.IndexOf('"', assignment);
            if (start < 0)
                return null;

            var end = text.IndexOf('"', start + 1);
            return end > start ? text[(start + 1)..end] : null;
        }

        // Run biçimi: "C:\...\uygulama.exe" --tray
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }

    private static string? TryReadLauncherTarget(string path)
    {
        try
        {
            return ExtractExecutablePath(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }
}
