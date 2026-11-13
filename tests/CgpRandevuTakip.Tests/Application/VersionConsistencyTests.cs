using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Tests.Application;

public class VersionConsistencyTests
{
    [Fact]
    public void AppConstantsVersion_MatchesAssemblyVersion()
    {
        var parts = AppConstants.Version.Split('.');
        Assert.Equal(3, parts.Length);

        var assembly = typeof(AppConstants).Assembly.GetName().Version;
        Assert.NotNull(assembly);

        Assert.Equal(int.Parse(parts[0]), assembly!.Major);
        Assert.Equal(int.Parse(parts[1]), assembly.Minor);
        Assert.Equal(int.Parse(parts[2]), assembly.Build);
    }

    [Fact]
    public void SchemaVersion_IsPositive()
        => Assert.True(AppConstants.SchemaVersion > 0);
}
