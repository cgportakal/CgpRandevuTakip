using CgpRandevuTakip.Domain.Rules;
using Xunit;

namespace CgpRandevuTakip.Tests.Domain;

public class BackupFileNameBuilderTests
{
    [Fact]
    public void Build_ReturnsExpectedName()
    {
        var timestamp = new DateTime(2026, 11, 6, 14, 30, 5);

        var fileName = BackupFileNameBuilder.Build(timestamp);

        Assert.Equal("CGP_RandevuTakip_Yedek_2026-11-06_14-30-05.zip", fileName);
        Assert.True(BackupFileNameBuilder.IsValid(fileName));
    }

    [Theory]
    [InlineData("CGP_RandevuTakip_Yedek_2026-11-06_14-30-05.zip", true)]
    [InlineData("CGP_RandevuTakip_Yedek_2026-13-06_14-30-05.zip", true)]
    [InlineData("Baska_Yedek_2026-11-06_14-30-05.zip", false)]
    [InlineData("CGP_RandevuTakip_Yedek_2026/11/06_14:30:05.zip", false)]
    [InlineData("CGP_RandevuTakip_Yedek_2026-11-06.zip", false)]
    [InlineData("", false)]
    public void IsValid_ChecksFormat(string fileName, bool expected)
    {
        Assert.Equal(expected, BackupFileNameBuilder.IsValid(fileName));
    }
}
