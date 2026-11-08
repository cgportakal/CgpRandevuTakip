namespace CgpRandevuTakip.Application.Abstractions;

/// <summary>Uygulamanın dosya sistemi konumları. Uygulama katmanı fiziksel yolları bilmez.</summary>
public interface IAppEnvironment
{
    string RootDirectory { get; }
    string DatabasePath { get; }
    string LogsDirectory { get; }
    string DefaultBackupDirectory { get; }
}
