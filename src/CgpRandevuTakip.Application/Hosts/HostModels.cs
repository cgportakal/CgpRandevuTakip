using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Hosts;

public sealed record HostDto(
    string Id,
    string DisplayName,
    string Username,
    UserRole Role,
    bool IsLoginEnabled,
    bool IsActive,
    int AppointmentCount,
    string? TitleId = null,
    string? TitleName = null)
{
    /// <summary>Kompakt yerlerde "Ad Soyad (Ünvan)" biçimini verir.</summary>
    public string DisplayNameWithTitle => string.IsNullOrWhiteSpace(TitleName)
        ? DisplayName
        : $"{DisplayName} ({TitleName})";
}

public sealed record HostCreateInput(
    string DisplayName,
    bool LoginEnabled,
    string? Username,
    string? Password,
    string? ConfirmPassword,
    string? TitleId = null);

public sealed record HostUpdateInput(
    string DisplayName,
    bool IsActive,
    string? TitleId = null,
    bool ClearTitle = false);
