using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Tests.Domain;

public class AppVersionComparerTests
{
    [Theory]
    [InlineData("v1.2.0", "1.1.2", true)]
    [InlineData("1.1.3", "1.1.2", true)]
    [InlineData("v1.1.2", "1.1.2", false)]
    [InlineData("v1.1.0", "1.1.2", false)]
    [InlineData("v2.0.0-beta.1", "1.9.9", true)]
    [InlineData("bozuk", "1.1.2", false)]
    [InlineData(null, "1.1.2", false)]
    public void IsNewer_ComparesVersions(string? candidate, string current, bool expected)
    {
        Assert.Equal(expected, AppVersionComparer.IsNewer(candidate, current));
    }
}
