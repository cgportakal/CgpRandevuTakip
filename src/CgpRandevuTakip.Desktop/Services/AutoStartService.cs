using System.IO;
using System.Text;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Windows oturumu açıldığında uygulamayı gizli (--tray) başlatır.
/// Kurulu sürümde HKCU Run, taşınabilir sürümde Başlangıç klasöründe VBS başlatıcı kullanılır.
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CgpRandevuTakip";
    private const string StartupFileName = "CGP - Randevu Takip.vbs";
    private const string LegacyShortcutName = "CGP - Randevu Takip.lnk";

    private static string StartupFilePath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupFileName);

    public bool IsEnabled()
    {
        if (AppPaths.IsPortable)
            return File.Exists(StartupFilePath);

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public void SetEnabled(bool enabled, string startMode)
    {
        if (AppPaths.IsPortable)
        {
            if (enabled)
                CreatePortableLauncher();
            else
                DeletePortableLauncher();
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            if (key.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath ?? string.Empty;
        var arguments = startMode == "tray" ? "--tray" : string.Empty;
        var command = string.IsNullOrWhiteSpace(arguments)
            ? exePath
            : $"\"{exePath}\" {arguments}";

        key.SetValue(ValueName, command);
    }

    private static void CreatePortableLauncher()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
                return;

            var script = string.Join(Environment.NewLine,
                "Set sh = CreateObject(\"WScript.Shell\")",
                $"sh.Run \"\"\"{exePath}\"\" --tray\", 0, False");

            File.WriteAllText(StartupFilePath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                LegacyShortcutName);
            if (File.Exists(legacy))
                File.Delete(legacy);
        }
        catch
        {
            // Başlatıcı oluşturulamazsa ayar kapalı görünür; kullanıcı yeniden deneyebilir.
        }
    }

    private static void DeletePortableLauncher()
    {
        try
        {
            if (File.Exists(StartupFilePath))
                File.Delete(StartupFilePath);

            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                LegacyShortcutName);
            if (File.Exists(legacy))
                File.Delete(legacy);
        }
        catch
        {
            // Başlatıcı silinemezse bir sonraki denemede ele alınır.
        }
    }
}
