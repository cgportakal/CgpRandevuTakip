using System.Diagnostics;
using System.IO;
using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Desktop.Services;

public interface IBitLockerService
{
    Task<BitLockerProtectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Windows BitLocker koruma durumunu sorgular. Yönetici hakkı yoksa veya cmdlet yoksa
/// "Bilinmiyor" döner; uygulama bu durumda kullanıcıyı bilgilendirir.
/// </summary>
public sealed class BitLockerService : IBitLockerService
{
    public async Task<BitLockerProtectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell",
                    "v1.0",
                    "powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -Command \"try { (Get-BitLockerVolume -MountPoint $env:SystemDrive).ProtectionStatus } catch { 'Unknown' }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return BitLockerProtectionStatus.Unknown;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

            var output = await process.StandardOutput.ReadToEndAsync(linked.Token);
            await process.WaitForExitAsync(linked.Token);
            return BitLockerStatusParser.Parse(output);
        }
        catch
        {
            return BitLockerProtectionStatus.Unknown;
        }
    }
}
