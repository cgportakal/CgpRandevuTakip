namespace CgpRandevuTakip.Application.Auth;

/// <summary>
/// Oturum kilidi politikası. Kullanıcı 30 dakika işlem yapmazsa oturum kilitlenir (FR-AUTH-05).
/// </summary>
public static class SessionLockPolicy
{
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(30);

    public static bool ShouldLock(
        DateTime lastActivityUtc,
        DateTime nowUtc,
        TimeSpan? idleTimeout = null)
    {
        var timeout = idleTimeout ?? DefaultIdleTimeout;
        if (timeout <= TimeSpan.Zero)
            return false;

        var idle = nowUtc - lastActivityUtc;
        return idle >= timeout;
    }

    public static TimeSpan Remaining(
        DateTime lastActivityUtc,
        DateTime nowUtc,
        TimeSpan? idleTimeout = null)
    {
        var timeout = idleTimeout ?? DefaultIdleTimeout;
        var remaining = timeout - (nowUtc - lastActivityUtc);
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}
