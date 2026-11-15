using System.IO;
using CgpRandevuTakip.Desktop.Services;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.Tests;

public class AutoStartServiceTests : IDisposable
{
    private const string AppExe = @"C:\Uygulama\CgpRandevuTakip.Desktop.exe";

    private readonly string _startupDirectory;
    private readonly string _runKeyPath;
    private readonly string _testRunRoot;

    public AutoStartServiceTests()
    {
        _startupDirectory = Path.Combine(Path.GetTempPath(), "cgp-autostart-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_startupDirectory);

        _testRunRoot = $@"Software\CgpRandevuTakip\Tests\{Guid.NewGuid():N}";
        _runKeyPath = _testRunRoot + @"\Run";
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_testRunRoot, throwOnMissingSubKey: false);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }

        try
        {
            if (Directory.Exists(_startupDirectory))
                Directory.Delete(_startupDirectory, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }

    [Fact]
    public void BuildPortableScript_ContainsSilentGuardAndTrayArgument()
    {
        var script = AutoStartService.BuildPortableScript(AppExe);

        Assert.Contains("On Error Resume Next", script);
        Assert.Contains("fso.FileExists(exePath)", script);
        Assert.Contains("--tray", script);
        Assert.Contains(AppExe, script);
    }

    [Fact]
    public void PortableEnabled_WritesLauncherThatSurvivesMissingTarget()
    {
        var service = CreateService(portable: true);

        service.SetEnabled(true, "tray");

        var launcher = Path.Combine(_startupDirectory, "CGP - Randevu Takip.vbs");
        Assert.True(File.Exists(launcher));

        var content = File.ReadAllText(launcher);
        Assert.Contains("fso.FileExists", content);
        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void PortableDisabled_RemovesLauncher()
    {
        var service = CreateService(portable: true);
        service.SetEnabled(true, "tray");

        service.SetEnabled(false, "tray");

        Assert.False(File.Exists(Path.Combine(_startupDirectory, "CGP - Randevu Takip.vbs")));
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void InstalledEnabled_WritesRunValueAndRemovesPortableLauncher()
    {
        var launcher = Path.Combine(_startupDirectory, "CGP - Randevu Takip.vbs");
        File.WriteAllText(launcher, AutoStartService.BuildPortableScript(AppExe));

        var service = CreateService(portable: false);

        service.SetEnabled(true, "tray");

        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        var value = key?.GetValue("CgpRandevuTakip") as string;

        Assert.NotNull(value);
        Assert.Contains(AppExe, value);
        Assert.Contains("--tray", value);
        Assert.False(File.Exists(launcher));
        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void InstalledDisabled_RemovesRunValue()
    {
        var service = CreateService(portable: false);
        service.SetEnabled(true, "tray");

        service.SetEnabled(false, "tray");

        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        Assert.Null(key?.GetValue("CgpRandevuTakip"));
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Repair_RemovesLauncherPointingToMissingTarget()
    {
        var launcher = Path.Combine(_startupDirectory, "CGP - Randevu Takip.vbs");
        File.WriteAllText(
            launcher,
            AutoStartService.BuildPortableScript(@"D:\Silinmis\Kopya\CgpRandevuTakip.Desktop.exe"));

        var service = CreateService(portable: true);

        service.Repair(enabled: false, startMode: "tray");

        Assert.False(File.Exists(launcher));
    }

    [Fact]
    public void Repair_RemovesEmptyRunValue()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_runKeyPath))
        {
            key.SetValue("CgpRandevuTakip", string.Empty);
        }

        var service = CreateService(portable: false);

        service.Repair(enabled: false, startMode: "tray");

        using var read = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        Assert.Null(read?.GetValue("CgpRandevuTakip"));
    }

    [Theory]
    [InlineData("\"C:\\Klasor\\CgpRandevuTakip.Desktop.exe\" --tray", @"C:\Klasor\CgpRandevuTakip.Desktop.exe")]
    [InlineData("C:\\Klasor\\CgpRandevuTakip.Desktop.exe --tray", @"C:\Klasor\CgpRandevuTakip.Desktop.exe")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractExecutablePath_HandlesRunFormats(string? command, string? expected)
        => Assert.Equal(expected, AutoStartService.ExtractExecutablePath(command));

    private AutoStartService CreateService(bool portable)
        => new(
            _startupDirectory,
            _runKeyPath,
            "CgpRandevuTakip",
            () => AppExe,
            () => portable);
}
