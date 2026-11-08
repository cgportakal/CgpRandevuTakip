using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Infrastructure.Persistence;

public sealed class WindowsAppEnvironment : IAppEnvironment
{
    public string RootDirectory => AppPaths.Root;
    public string DatabasePath => AppPaths.DatabasePath;
    public string LogsDirectory => AppPaths.LogsDirectory;
    public string DefaultBackupDirectory => AppPaths.DefaultBackupDirectory;
}
