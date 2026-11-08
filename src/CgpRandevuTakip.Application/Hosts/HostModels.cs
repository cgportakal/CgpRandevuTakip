using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Hosts;

public sealed record HostDto(
    string Id,
    string DisplayName,
    string Username,
    UserRole Role,
    bool IsLoginEnabled,
    bool IsActive,
    int AppointmentCount);

public sealed record HostCreateInput(
    string DisplayName,
    bool LoginEnabled,
    string? Username,
    string? Password,
    string? ConfirmPassword);

public sealed record HostUpdateInput(
    string DisplayName,
    bool IsActive);
