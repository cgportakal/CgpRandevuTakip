using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Auth;

public sealed record UserSession(
    string UserId,
    string Username,
    string DisplayName,
    UserRole Role);

public sealed record SetupRequest(
    string Username,
    string DisplayName,
    string Password,
    string ConfirmPassword);

public sealed record SetupResult(bool Success, string? Error, UserSession? Session)
{
    public static SetupResult Ok(UserSession session) => new(true, null, session);
    public static SetupResult Fail(string error) => new(false, error, null);
}

public sealed record LoginResult(
    bool Success,
    string? Error,
    int? LockoutRemainingMinutes,
    UserSession? Session)
{
    public static LoginResult Ok(UserSession session) => new(true, null, null, session);
    public static LoginResult Fail(string error, int? lockoutMinutes = null)
        => new(false, error, lockoutMinutes, null);
}
