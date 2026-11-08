using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Tests.Domain;

public sealed class AppConstantsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveInstitutionName_FallsBackToBrandWhenEmpty(string? value)
    {
        Assert.Equal(AppConstants.DefaultInstitutionName, AppConstants.ResolveInstitutionName(value));
    }

    [Fact]
    public void ResolveInstitutionName_TrimsConfiguredName()
    {
        Assert.Equal("CGP Koleji", AppConstants.ResolveInstitutionName("  CGP Koleji  "));
    }

    [Fact]
    public void ProductIdentity_IsCgp()
    {
        Assert.Equal("CGP - Randevu Takip", AppConstants.ProductName);
        Assert.Equal("CGP_RandevuTakip", AppConstants.ProductCode);
    }
}
