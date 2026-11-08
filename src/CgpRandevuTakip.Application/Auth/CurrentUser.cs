using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private UserSession? _session;

    public string? UserId => _session?.UserId;
    public string Username => _session?.Username ?? string.Empty;
    public string DisplayName => _session?.DisplayName ?? "Misafir";
    public UserRole? Role => _session?.Role;
    public bool IsAuthenticated => _session is not null;

    public void SignIn(UserSession session) => _session = session;

    public void SignOut() => _session = null;

    public bool IsInRole(params UserRole[] roles)
        => _session is not null && roles.Contains(_session.Role);
}
