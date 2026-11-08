using CgpRandevuTakip.Application.Reminders;
using Xunit;

namespace CgpRandevuTakip.Tests.Application;

public class NotificationPolicyTests
{
    [Theory]
    [InlineData("22:00", true)]
    [InlineData("23:30", true)]
    [InlineData("02:00", true)]
    [InlineData("06:59", true)]
    [InlineData("07:00", false)]
    [InlineData("12:00", false)]
    [InlineData("21:59", false)]
    public void IsQuietNow_CrossMidnight(string time, bool expected)
    {
        var local = DateTime.ParseExact($"2026-11-08 {time}", "yyyy-MM-dd HH:mm", null);

        var result = NotificationPolicy.IsQuietNow(
            local,
            start: TimeSpan.FromHours(22),
            end: TimeSpan.FromHours(7),
            enabled: true);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("22:00", false)]
    [InlineData("12:00", false)]
    public void IsQuietNow_Disabled_AlwaysFalse(string time, bool _)
    {
        var local = DateTime.ParseExact($"2026-11-08 {time}", "yyyy-MM-dd HH:mm", null);

        Assert.False(NotificationPolicy.IsQuietNow(
            local,
            TimeSpan.FromHours(22),
            TimeSpan.FromHours(7),
            enabled: false));
    }

    [Fact]
    public void IsQuietNow_StartEqualsEnd_IsDisabled()
    {
        Assert.False(NotificationPolicy.IsQuietNow(
            new DateTime(2026, 11, 8, 23, 0, 0),
            TimeSpan.FromHours(20),
            TimeSpan.FromHours(20),
            enabled: true));
    }
}
