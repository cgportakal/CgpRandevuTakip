using CgpRandevuTakip.Desktop.Services;

namespace CgpRandevuTakip.Desktop.Tests;

public class ReleaseNotesFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToPlainText_EmptyInput_ReturnsEmpty(string? notes)
    {
        Assert.Equal(string.Empty, ReleaseNotesFormatter.ToPlainText(notes));
    }

    [Fact]
    public void ToPlainText_RemovesMarkdownMarkers()
    {
        const string notes = """
            ## 1.8.1 - Guncelleme duzeltmesi

            ### Duzeltildi
            - **Ilerleme penceresi:** dogrulama arka planda calisir.
            - Ayrinti: [docs/37](docs/37_UAT.md).
            """;

        var text = ReleaseNotesFormatter.ToPlainText(notes);

        Assert.Contains("1.8.1 - Guncelleme duzeltmesi", text);
        Assert.Contains("Duzeltildi", text);
        Assert.Contains("• Ilerleme penceresi: dogrulama arka planda calisir.", text);
        Assert.Contains("• Ayrinti: docs/37.", text);
        Assert.DoesNotContain("#", text);
        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("](", text);
    }

    [Fact]
    public void ToPlainText_RemovesCommentsAndHorizontalRules()
    {
        const string notes = """
            Baslik
            ---
            <!-- gizli not -->
            Govde
            """;

        var text = ReleaseNotesFormatter.ToPlainText(notes);

        Assert.Equal("Baslik\n\nGovde", text);
    }

    [Fact]
    public void ToPlainText_CollapsesRepeatedBlankLines()
    {
        var text = ReleaseNotesFormatter.ToPlainText("Bir\n\n\n\nIki");

        Assert.Equal("Bir\n\nIki", text);
    }
}
