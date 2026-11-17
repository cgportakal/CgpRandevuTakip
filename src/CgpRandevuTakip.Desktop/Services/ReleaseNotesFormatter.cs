using System.Text;
using System.Text.RegularExpressions;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// GitHub sürüm notları Markdown olarak gelir; masaüstü pencereleri düz metin
/// gösterdiği için başlık, liste, kalın ve bağlantı işaretlerini sadeleştirir.
/// </summary>
public static class ReleaseNotesFormatter
{
    private static readonly Regex HtmlComment = new("<!--.*?-->", RegexOptions.Singleline);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]*\)");
    private static readonly Regex Bold = new(@"\*\*(.+?)\*\*|__(.+?)__");
    private static readonly Regex InlineCode = new("`([^`]+)`");
    private static readonly Regex RepeatedBlankLines = new(@"\n{3,}");
    private static readonly Regex Heading = new(@"^\s{0,3}#{1,6}\s*");
    private static readonly Regex HorizontalRule = new(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$");
    private static readonly Regex Bullet = new(@"^\s*[-*+]\s+");
    private static readonly Regex Quote = new(@"^\s*>\s?");

    /// <summary>Markdown sürüm notlarını pencerede okunur düz metne çevirir.</summary>
    public static string ToPlainText(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return string.Empty;

        var text = HtmlComment.Replace(
            notes.Replace("\r\n", "\n").Replace('\r', '\n'),
            string.Empty);

        var builder = new StringBuilder(text.Length);
        foreach (var rawLine in text.Split('\n'))
        {
            if (HorizontalRule.IsMatch(rawLine))
                continue;

            var line = Heading.Replace(rawLine, string.Empty, 1);
            line = Quote.Replace(line, string.Empty, 1);
            line = Bullet.Replace(line, "• ", 1);
            line = InlineCode.Replace(line, "$1");
            line = Link.Replace(line, "$1");
            line = Bold.Replace(line, match => match.Groups[1].Success
                ? match.Groups[1].Value
                : match.Groups[2].Value);

            builder.Append(line.TrimEnd()).Append('\n');
        }

        var result = RepeatedBlankLines.Replace(builder.ToString(), "\n\n");
        return result.Trim();
    }
}
