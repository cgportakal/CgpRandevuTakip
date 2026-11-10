using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Tests.Domain;

public class BitLockerStatusParserTests
{
    [Theory]
    [InlineData("On", BitLockerProtectionStatus.On)]
    [InlineData("Protection On", BitLockerProtectionStatus.On)]
    [InlineData("Off", BitLockerProtectionStatus.Off)]
    [InlineData("Protection Off", BitLockerProtectionStatus.Off)]
    [InlineData("Unknown", BitLockerProtectionStatus.Unknown)]
    [InlineData("Access denied", BitLockerProtectionStatus.Unknown)]
    [InlineData("", BitLockerProtectionStatus.Unknown)]
    public void Parse_MapsProtectionStatus(string output, BitLockerProtectionStatus expected)
    {
        Assert.Equal(expected, BitLockerStatusParser.Parse(output));
    }
}
