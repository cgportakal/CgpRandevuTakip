using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Tests.Domain;

public class SeriesScheduleCalculatorTests
{
    private static readonly DateTime Anchor = new(2026, 1, 31, 10, 30, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Weekly_AddsSevenDaysPerSlot()
    {
        var dates = SeriesScheduleCalculator.Build(Anchor, SeriesFrequency.Weekly, 4);

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 1, 31, 10, 30, 0),
                new DateTime(2026, 2, 7, 10, 30, 0),
                new DateTime(2026, 2, 14, 10, 30, 0),
                new DateTime(2026, 2, 21, 10, 30, 0)
            },
            dates);
    }

    [Fact]
    public void Biweekly_AddsFourteenDaysPerSlot()
    {
        var dates = SeriesScheduleCalculator.Build(Anchor, SeriesFrequency.Biweekly, 3);

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 1, 31, 10, 30, 0),
                new DateTime(2026, 2, 14, 10, 30, 0),
                new DateTime(2026, 2, 28, 10, 30, 0)
            },
            dates);
    }

    [Fact]
    public void Monthly_ClampsMonthEndButKeepsAnchorDay()
    {
        // 31 Ocak → 28 Şubat → 31 Mart (kırpma her ay çapa tarihten hesaplanır).
        var dates = SeriesScheduleCalculator.Build(Anchor, SeriesFrequency.Monthly, 3);

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 1, 31, 10, 30, 0),
                new DateTime(2026, 2, 28, 10, 30, 0),
                new DateTime(2026, 3, 31, 10, 30, 0)
            },
            dates);
    }

    [Fact]
    public void Monthly_FromLeapDayFallsBackToMonthEnd()
    {
        var anchor = new DateTime(2024, 2, 29, 14, 0, 0);
        var dates = SeriesScheduleCalculator.Build(anchor, SeriesFrequency.Monthly, 12);

        Assert.Equal(new DateTime(2024, 3, 29, 14, 0, 0), dates[1]);
        Assert.Equal(new DateTime(2024, 4, 29, 14, 0, 0), dates[2]);
        Assert.Equal(new DateTime(2025, 1, 29, 14, 0, 0), dates[11]);

        // Aynı çapa 12 ay sonra: 29 Şubat olmadığı için ay sonuna kırpılır.
        Assert.Equal(
            new DateTime(2025, 2, 28, 14, 0, 0),
            SeriesScheduleCalculator.AddMonthsClamped(anchor, 12));
    }

    [Fact]
    public void Monthly_CrossesYearBoundary()
    {
        var dates = SeriesScheduleCalculator.Build(
            new DateTime(2025, 12, 31, 9, 0, 0),
            SeriesFrequency.Monthly,
            2);

        Assert.Equal(new DateTime(2026, 1, 31, 9, 0, 0), dates[1]);
    }

    [Fact]
    public void AddMonthsClamped_KeepsTimeOfDay()
    {
        var result = SeriesScheduleCalculator.AddMonthsClamped(
            new DateTime(2026, 8, 31, 16, 45, 0),
            6);

        Assert.Equal(new DateTime(2027, 2, 28, 16, 45, 0), result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(13)]
    public void Build_RejectsCountOutsideRange(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SeriesScheduleCalculator.Build(Anchor, SeriesFrequency.Weekly, count));
    }

    [Fact]
    public void Build_RejectsUndefinedFrequency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SeriesScheduleCalculator.Build(Anchor, (SeriesFrequency)99, 3));
    }
}
