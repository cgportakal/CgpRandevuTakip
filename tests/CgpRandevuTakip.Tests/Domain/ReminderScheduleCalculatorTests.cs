using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;
using Xunit;

namespace CgpRandevuTakip.Tests.Domain;

public class ReminderScheduleCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_DefaultThirtyMinutes_ReturnsSinglePlan()
    {
        var start = Now.AddHours(3);

        var plans = ReminderScheduleCalculator.Calculate(start, new[] { 30 }, Now);

        var plan = Assert.Single(plans);
        Assert.Equal(30, plan.OffsetMinutes);
        Assert.Equal(start.AddMinutes(-30), plan.DueAtUtc);
    }

    [Fact]
    public void Calculate_MultipleOffsets_ReturnsSortedByDue()
    {
        var start = Now.AddDays(2);

        var plans = ReminderScheduleCalculator.Calculate(start, new[] { 30, 1440, 60 }, Now);

        Assert.Equal(3, plans.Count);
        Assert.Equal(new[] { 1440, 60, 30 }, plans.Select(p => p.OffsetMinutes).ToArray());
    }

    [Fact]
    public void Calculate_PastReminders_AreSkipped()
    {
        var start = Now.AddMinutes(20);

        var plans = ReminderScheduleCalculator.Calculate(start, new[] { 1440, 60, 30 }, Now);

        Assert.Empty(plans);
    }

    [Fact]
    public void Calculate_InvalidOffset_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReminderScheduleCalculator.Calculate(
                Now.AddDays(10), new[] { ReminderScheduleCalculator.MaxOffsetMinutes + 1 }, Now));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReminderScheduleCalculator.Calculate(Now.AddDays(1), new[] { 0 }, Now));
    }

    [Fact]
    public void Calculate_CustomOffset_IsSupported()
    {
        var start = Now.AddDays(2);

        var plans = ReminderScheduleCalculator.Calculate(start, new[] { 15 }, Now);

        var plan = Assert.Single(plans);
        Assert.Equal(15, plan.OffsetMinutes);
    }

    [Fact]
    public void GetCallReminderTimes_ReturnsThirtyAndFiveMinutesBefore()
    {
        var start = new DateTime(2026, 11, 6, 14, 30, 0, DateTimeKind.Utc);

        var times = ReminderScheduleCalculator.GetCallReminderTimes(start);

        Assert.Equal(new[] { start.AddMinutes(-30), start.AddMinutes(-5) }, times);
    }

    [Fact]
    public void BuildDedupKey_IsStable()
    {
        Assert.Equal("abc:30:1", ReminderScheduleCalculator.BuildDedupKey("abc", 30, 1));
    }

    [Fact]
    public void BuildDedupKey_CallKind_IncludesMarker()
    {
        Assert.Equal(
            "abc:call:5:1",
            ReminderScheduleCalculator.BuildDedupKey("abc", ReminderKind.Call, 5, 1));
        Assert.Equal(
            "abc:30:1",
            ReminderScheduleCalculator.BuildDedupKey("abc", ReminderKind.Appointment, 30, 1));
    }

    [Fact]
    public void CalculateCallReminders_ReturnsThirtyAndFiveWhenFuture()
    {
        var start = Now.AddHours(2);

        var plans = ReminderScheduleCalculator.CalculateCallReminders(start, Now);

        Assert.Equal(new[] { 30, 5 }, plans.Select(p => p.OffsetMinutes).ToArray());
        Assert.All(plans, p => Assert.True(p.DueAtUtc > Now));
    }

    [Fact]
    public void CalculateCallReminders_SkipsPastOffsets()
    {
        var start = Now.AddMinutes(4);

        var plans = ReminderScheduleCalculator.CalculateCallReminders(start, Now);

        Assert.Empty(plans);
    }
}
