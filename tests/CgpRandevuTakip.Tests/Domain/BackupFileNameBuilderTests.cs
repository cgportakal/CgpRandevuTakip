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

    [Fact]
    public void Build_EncryptedUsesCgpbakExtension()
    {
        var timestamp = new DateTime(2026, 11, 6, 14, 30, 5);

        var fileName = BackupFileNameBuilder.Build(timestamp, encrypted: true);

        Assert.Equal("CGP_RandevuTakip_Yedek_2026-11-06_14-30-05.cgpbak", fileName);
        Assert.True(BackupFileNameBuilder.IsValid(fileName));
        Assert.True(BackupFileNameBuilder.IsEncrypted(fileName));
        Assert.False(BackupFileNameBuilder.IsEncrypted(
            "CGP_RandevuTakip_Yedek_2026-11-06_14-30-05.zip"));
    }

    [Theory]
    [InlineData("CGP_RandevuTakip_Yedek_2026-11-06_14-30-05.zip", true)]
    [InlineData("CGP_RandevuTakip_Yedek_2026-11-06_14-30-05.cgpbak", true)]
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
