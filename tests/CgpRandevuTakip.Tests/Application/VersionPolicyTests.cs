using CgpRandevuTakip.Application.Updates;

namespace CgpRandevuTakip.Tests.Application;

public class VersionPolicyTests
{
    [Theory]
    [InlineData("1.7.0", "1.8.0", true)]
    [InlineData("1.7.0", "1.7.0", false)]
    [InlineData("1.8.0", "1.7.0", false)]
    [InlineData("1.7.9", "1.8.0", true)]
    [InlineData("2.0.0", "1.99.99", false)]
    public void IsForcedUpdate_ComparesSemanticVersions(
        string current,
        string minimum,
        bool expected)
    {
        Assert.Equal(expected, VersionPolicyEvaluator.IsForcedUpdate(current, minimum));
    }

    [Fact]
    public void IsForcedDesktopUpdate_RequiresMinimumPolicy()
    {
        var policy = new VersionPolicy(
            "1.8.0",
            "1.0.0",
            "1.0.0",
            "1.8.0",
            "1.0.0",
            "1.8.0");

        Assert.True(VersionPolicyEvaluator.IsForcedDesktopUpdate("1.7.0", policy));
        Assert.False(VersionPolicyEvaluator.IsForcedDesktopUpdate("1.8.0", policy));
        Assert.False(VersionPolicyEvaluator.IsForcedDesktopUpdate("1.7.0", null));
        Assert.False(VersionPolicyEvaluator.IsForcedDesktopUpdate("1.7.0", VersionPolicy.Empty));
    }
}
