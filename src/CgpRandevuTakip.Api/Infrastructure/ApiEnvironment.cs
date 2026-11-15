using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Api.Infrastructure;

/// <summary>Sunucu tarafı dosya konumları. Masaüstü kullanıcı profiline bağımlı değildir.</summary>
public sealed class ApiEnvironment : IAppEnvironment
{
    public ApiEnvironment(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        LogsDirectory = Path.Combine(rootDirectory, "logs");
        DefaultBackupDirectory = Path.Combine(rootDirectory, "backups");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DefaultBackupDirectory);
    }

    public string RootDirectory { get; }
    public string DatabasePath => Path.Combine(RootDirectory, "cgp.db");
    public string LogsDirectory { get; }
    public string DefaultBackupDirectory { get; }
    public string BrandingDirectory => Path.Combine(RootDirectory, "branding");
}
