using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    string Username { get; }
    string DisplayName { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(params UserRole[] roles);
}
