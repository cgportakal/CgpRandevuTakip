using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Application.Updates;

/// <summary>Sunucudan gelen minimum/önerilen istemci sürümleri.</summary>
public sealed record VersionPolicy(
    string? MinDesktopVersion,
    string? MinMobileVersion,
    string? MinWebVersion,
    string? LatestDesktopVersion,
    string? LatestMobileVersion,
    string? LatestWebVersion)
{
    public static VersionPolicy Empty { get; } = new(null, null, null, null, null, null);
}

public static class VersionPolicyEvaluator
{
    /// <summary>Mevcut masaüstü sürümü minimumun altındaysa zorunlu güncelleme gerekir.</summary>
    public static bool IsForcedDesktopUpdate(string currentVersion, VersionPolicy? policy)
        => policy?.MinDesktopVersion is { } minimum
           && AppVersionComparer.IsNewer(minimum, currentVersion);

    /// <summary>Mevcut istemci sürümü minimumun altındaysa zorunlu güncelleme gerekir.</summary>
    public static bool IsForcedUpdate(string currentVersion, string? minimumVersion)
        => !string.IsNullOrWhiteSpace(minimumVersion)
           && AppVersionComparer.IsNewer(minimumVersion, currentVersion);
}
