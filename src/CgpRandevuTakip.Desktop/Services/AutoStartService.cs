using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Windows oturumu açılışında uygulamayı başlatma kaydını yönetir (HKCU Run).
/// Kullanıcı başına çalışır; yönetici hakkı gerektirmez.
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CgpRandevuTakip";

    public bool IsEnabled()
    {
        if (AppPaths.IsPortable)
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public void SetEnabled(bool enabled, string startMode)
    {
        // Taşınabilir modda kayıt defterine yazılmaz; uygulama taşındığında kayıt bozulmasın.
        if (AppPaths.IsPortable)
            return;

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
}
