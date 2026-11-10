using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Tests.Domain;

public class AutoBackupScheduleTests
{
    [Fact]
    public void BeforeNoon_DoesNotRun()
    {
        var now = new DateTime(2026, 11, 10, 11, 0, 0);

        Assert.False(AutoBackupSchedule.ShouldRun(now, 0, sharedAfterNoon: false));
    }

    [Fact]
    public void FirstBackupRunsAtNoon()
    {
        var now = new DateTime(2026, 11, 10, 12, 30, 0);

        Assert.True(AutoBackupSchedule.ShouldRun(now, 0, sharedAfterNoon: false));
    }

    [Fact]
    public void SecondBackupRunsImmediatelyAfterAfternoonShare()
    {
        var now = new DateTime(2026, 11, 10, 14, 0, 0);

        Assert.True(AutoBackupSchedule.ShouldRun(now, 1, sharedAfterNoon: true));
    }

    [Fact]
    public void SecondBackupWaitsForEveningWithoutShare()
    {
        var now = new DateTime(2026, 11, 10, 15, 0, 0);
        Assert.False(AutoBackupSchedule.ShouldRun(now, 1, sharedAfterNoon: false));

        var evening = new DateTime(2026, 11, 10, 18, 0, 0);
        Assert.True(AutoBackupSchedule.ShouldRun(evening, 1, sharedAfterNoon: false));
    }

    [Fact]
    public void ThirdBackupDoesNotRun()
    {
        var now = new DateTime(2026, 11, 10, 20, 0, 0);

        Assert.False(AutoBackupSchedule.ShouldRun(now, 2, sharedAfterNoon: true));
    }
}
