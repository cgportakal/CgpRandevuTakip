using CgpRandevuTakip.Application.Auth;
using Xunit;

namespace CgpRandevuTakip.Tests.Application;

public class SessionLockPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ShouldLock_BeforeTimeout_ReturnsFalse()
    {
        Assert.False(SessionLockPolicy.ShouldLock(Now.AddMinutes(-29), Now));
    }

    [Fact]
    public void ShouldLock_AtTimeout_ReturnsTrue()
    {
        Assert.True(SessionLockPolicy.ShouldLock(Now.AddMinutes(-30), Now));
    }

    [Fact]
    public void ShouldLock_AfterTimeout_ReturnsTrue()
    {
        Assert.True(SessionLockPolicy.ShouldLock(Now.AddHours(-2), Now));
    }

    [Fact]
    public void ShouldLock_NonPositiveTimeout_DisablesLocking()
    {
        Assert.False(SessionLockPolicy.ShouldLock(
            Now.AddDays(-1),
            Now,
            TimeSpan.Zero));
    }

    [Fact]
    public void ShouldLock_CustomTimeout_IsRespected()
    {
        var timeout = TimeSpan.FromMinutes(5);

        Assert.False(SessionLockPolicy.ShouldLock(Now.AddMinutes(-4), Now, timeout));
        Assert.True(SessionLockPolicy.ShouldLock(Now.AddMinutes(-5), Now, timeout));
    }

    [Fact]
    public void Remaining_NeverNegative()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), SessionLockPolicy.Remaining(Now.AddMinutes(-20), Now));
        Assert.Equal(TimeSpan.Zero, SessionLockPolicy.Remaining(Now.AddMinutes(-45), Now));
    }
}
