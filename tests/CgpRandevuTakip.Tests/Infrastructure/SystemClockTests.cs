using CgpRandevuTakip.Infrastructure.Time;

namespace CgpRandevuTakip.Tests.Infrastructure;

public class SystemClockTests
{
    [Fact]
    public void ToLocal_UsesSystemTimeZone()
    {
        var clock = new SystemClock();
        var utc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var local = clock.ToLocal(utc);

        Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local), local);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(utc), local - utc);
    }

    [Fact]
    public void ToUtc_RoundTripsLocalTime()
    {
        var clock = new SystemClock();
        var utc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var local = clock.ToLocal(utc);

        var result = clock.ToUtc(local);

        Assert.Equal(utc, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ToUtc_HandlesDateBoundary()
    {
        var clock = new SystemClock();
        var local = new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Unspecified);

        var utc = clock.ToUtc(local);

        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local), utc);
    }
}
