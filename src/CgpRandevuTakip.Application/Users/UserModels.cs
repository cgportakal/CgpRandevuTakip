using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Users;

public sealed record UserDto(
    string Id,
    string Username,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    bool IsLoginEnabled,
    DateTime? LastLoginAtLocal,
    DateTime CreatedAtLocal,
    string? Email = null)
{
    public string RoleText => Role switch
    {
        UserRole.Admin => "Yönetici",
        UserRole.Secretary => "Sekreter",
        _ => "Görüşmeci"
    };

    public string LastLoginText => LastLoginAtLocal is null
        ? "—"
        : LastLoginAtLocal.Value.ToString("dd/MM/yyyy HH:mm");

    public string StatusText => !IsActive
        ? "Pasif"
        : IsLoginEnabled ? "Aktif" : "Girişsiz";
}

public sealed record UserCreateInput(
    string Username,
    string DisplayName,
    UserRole Role,
    bool IsLoginEnabled,
    string? Password,
    string? ConfirmPassword,
    string? Email = null);

public sealed record UserUpdateInput(
    string DisplayName,
    UserRole Role,
    bool IsActive,
    bool IsLoginEnabled,
    string? Email = null);

public sealed record UserResetPasswordInput(
    string Password,
    string ConfirmPassword);
