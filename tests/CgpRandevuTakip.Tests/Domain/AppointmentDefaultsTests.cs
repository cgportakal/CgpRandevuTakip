using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Tests.Domain;

public class AppointmentDefaultsTests
{
    [Fact]
    public void NextStart_AddsOneHourAndRoundsUpToQuarter()
    {
        var now = new DateTime(2026, 11, 10, 13, 7, 0);

        var start = AppointmentDefaults.NextStart(now);

        Assert.Equal(new DateTime(2026, 11, 10, 14, 15, 0), start);
    }

    [Fact]
    public void NextStart_UsesExactQuarterWhenAlreadyAligned()
    {
        var now = new DateTime(2026, 11, 10, 9, 0, 0);

        var start = AppointmentDefaults.NextStart(now);

        Assert.Equal(new DateTime(2026, 11, 10, 10, 0, 0), start);
    }

    [Fact]
    public void NextStart_ClampsEarlyMorningToSix()
    {
        var now = new DateTime(2026, 11, 10, 4, 30, 0);

        var start = AppointmentDefaults.NextStart(now);

        Assert.Equal(new DateTime(2026, 11, 10, 6, 0, 0), start);
    }

    [Fact]
    public void NextStart_OverflowingWorkdayMovesToNextMorning()
    {
        var now = new DateTime(2026, 11, 10, 20, 30, 0);

        var start = AppointmentDefaults.NextStart(now);

        Assert.Equal(new DateTime(2026, 11, 11, 6, 0, 0), start);
    }

    [Fact]
    public void NextStart_CrossingMidnightMovesToNextMorning()
    {
        var now = new DateTime(2026, 11, 10, 23, 15, 0);

        var start = AppointmentDefaults.NextStart(now);

        Assert.Equal(new DateTime(2026, 11, 11, 6, 0, 0), start);
    }

    [Fact]
    public void NextStart_UsesConfiguredWorkHours()
    {
        var now = new DateTime(2026, 11, 10, 16, 30, 0);

        var start = AppointmentDefaults.NextStart(
            now,
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(17));

        Assert.Equal(new DateTime(2026, 11, 11, 8, 0, 0), start);
    }

    [Fact]
    public void NextStart_AllowsOutOfHoursWhenEnabled()
    {
        var now = new DateTime(2026, 11, 10, 22, 0, 0);

        var start = AppointmentDefaults.NextStart(
            now,
            allowOutOfHours: true);

        Assert.Equal(new DateTime(2026, 11, 10, 23, 0, 0), start);
    }

    [Fact]
    public void BuildTimeOptions_CoversWorkHoursInQuarterHours()
    {
        var options = AppointmentDefaults.BuildTimeOptions(
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(45)),
            allowOutOfHours: false);

        Assert.Equal(60, options.Count);
        Assert.Equal("06:00", options[0]);
        Assert.Equal("20:45", options[^1]);
    }
}
