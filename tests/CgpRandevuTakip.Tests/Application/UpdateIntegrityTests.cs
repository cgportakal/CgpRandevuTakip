using System.IO;
using CgpRandevuTakip.Application.Updates;

namespace CgpRandevuTakip.Tests.Application;

public class UpdateIntegrityTests
{
    [Fact]
    public void ComputeSha256_MatchesKnownValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cgp-integrity-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "abc");

        try
        {
            // "abc" için bilinen SHA-256 değeri.
            const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
            Assert.Equal(expected, UpdateIntegrity.ComputeSha256(path));
            Assert.True(UpdateIntegrity.Matches(path, expected));
            Assert.True(UpdateIntegrity.Matches(path, "SHA256:" + expected.ToUpperInvariant()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Matches_ReturnsFalseForDifferentDigest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cgp-integrity-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "abc");

        try
        {
            Assert.False(UpdateIntegrity.Matches(path, new string('0', 64)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("sha256:ABCDEF", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("zzzz", null)]
    public void NormalizeDigest_RejectsInvalidValues(string? input, string? _)
        => Assert.Null(UpdateIntegrity.NormalizeDigest(input));

    [Fact]
    public void NormalizeDigest_AcceptsPrefixedValue()
        => Assert.Equal(new string('a', 64), UpdateIntegrity.NormalizeDigest("sha256:" + new string('A', 64)));

    [Fact]
    public void NormalizeDigest_AcceptsPlainValue()
        => Assert.Equal(new string('b', 64), UpdateIntegrity.NormalizeDigest(new string('b', 64)));
}
