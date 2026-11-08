using CgpRandevuTakip.Domain.ValueObjects;
using Xunit;

namespace CgpRandevuTakip.Tests.Domain;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0532 123 45 67", "+905321234567")]
    [InlineData("5321234567", "+905321234567")]
    [InlineData("+90 532 123 45 67", "+905321234567")]
    [InlineData("0090 532 123 45 67", "+905321234567")]
    [InlineData("+1 (202) 555-0143", "+12025550143")]
    public void TryNormalize_ValidNumbers_ReturnsE164(string input, string expected)
    {
        var ok = PhoneNumber.TryNormalize(input, out var normalized);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("abc")]
    [InlineData("+90 532 123 45 67 89 01 23")]
    public void TryNormalize_InvalidNumbers_ReturnsFalse(string input)
    {
        var ok = PhoneNumber.TryNormalize(input, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void ToWhatsAppDigits_RemovesPlus()
    {
        Assert.Equal("905321234567", PhoneNumber.ToWhatsAppDigits("+905321234567"));
    }
}
