namespace CgpRandevuTakip.Desktop.Services;

public enum UpdateProgressPhase
{
    Preparing = 0,
    Downloading = 1,
    Verifying = 2,
    Extracting = 3,
    PreparingRollback = 4,
    StartingInstaller = 5
}

/// <summary>Güncelleme indirme/kurulum adımlarının ilerleme bilgisi.</summary>
public sealed record UpdateDownloadProgress(
    UpdateProgressPhase Phase,
    long BytesReceived,
    long? TotalBytes)
{
    public double? Percent => TotalBytes is > 0
        ? Math.Clamp(BytesReceived * 100.0 / TotalBytes.Value, 0, 100)
        : null;

    public string PhaseText => Phase switch
    {
        UpdateProgressPhase.Preparing => "Güncelleme hazırlanıyor…",
        UpdateProgressPhase.Downloading => "Güncelleme indiriliyor…",
        UpdateProgressPhase.Verifying => "İndirme doğrulanıyor…",
        UpdateProgressPhase.Extracting => "Güncelleme paketi açılıyor…",
        UpdateProgressPhase.PreparingRollback => "Geri dönüş noktası hazırlanıyor…",
        UpdateProgressPhase.StartingInstaller => "Kurulum başlatılıyor…",
        _ => "Güncelleme hazırlanıyor…"
    };
}
