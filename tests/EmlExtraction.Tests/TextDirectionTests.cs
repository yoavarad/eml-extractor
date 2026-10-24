using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EmlExtraction.Logics;
using MimeKit;

namespace EmlExtraction.Tests;

// Not parallel: the static generated Regex instances keep one cached runner; a concurrent
// test (any extraction) can hold it, so Match allocates a fresh runner and the zero-allocation
// test fails intermittently.
[Collection("NonParallel")]
public sealed partial class TextDirectionTests
{
    [Theory]
    [InlineData("<p>abc", "ltr")]
    [InlineData("<p>א", "rtl")]
    [InlineData("<p>א unclosed <b", "rtl")]
    [InlineData("<unclosed א text", "ltr")]
    [InlineData("a > א", "ltr")]
    [InlineData("> א b", "rtl")]
    [InlineData("<p><b></b></p>", "ltr")]
    [InlineData("<a title=\"א\">x</a>", "ltr")]
    [InlineData("<a title=\"א\">א</a>", "rtl")]
    [InlineData("<p>𐠀</p>", "rtl")]
    [InlineData("<p>𝐀</p>", "ltr")]
    [InlineData("<p>\uD83D</p>\uDE00 a", "ltr")]
    [InlineData("<p>\uD802</p>\uDC00 א", "rtl")]
    [InlineData("<p>‏</p>a", "rtl")]
    [InlineData("<p>‬ ⁨ 1 2</p>", "ltr")]
    [InlineData("x < y > א", "ltr")]
    [InlineData("<p>1 2 3</p> א", "rtl")]
    public void HtmlWithoutMarkupDirectionUsesFirstStrongOutsideTags(string html, string expected)
        => Assert.Equal(expected, TextDirection.Detect(html, html: true));

    [Fact]
    public void ScanMatchesLegacyStripTagsOnGeneratedInputs()
    {
        string[] pieces =
        [
            "<", ">", "<p>", "</p>", "<a title=\"", "\">", "a", "א", "\uD802", "\uDC00", "\uD83D", "\uDE00",
            " ", "1", "‏", "‎", "‬", "ا", "中"
        ];
        var rng = new Random(12345);
        for (var i = 0; i < 20000; i++)
        {
            var sb = new StringBuilder();
            var n = rng.Next(1, 9);
            for (var j = 0; j < n; j++)
                sb.Append(pieces[rng.Next(pieces.Length)]);
            var s = sb.ToString();
            Assert.True(Legacy(s) == TextDirection.Detect(s, html: true), s);
        }
    }

    [Fact]
    public void ScanMatchesLegacyOnEveryGoldenHtmlBody()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "corpus", "golden")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var checkedBodies = 0;
        var skipped = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(dir.FullName, "corpus", "golden"), "input.eml",
                     SearchOption.AllDirectories))
        {
            MimeMessage message;
            try
            {
                using var stream = File.OpenRead(path);
                message = MimeMessage.Load(stream);
            }
            catch (Exception)
            {
                skipped++;
                continue;
            }

            foreach (var part in message.BodyParts.OfType<TextPart>().Where(p => p.IsHtml))
            {
                string text;
                try { text = part.Text.TrimEnd('\r', '\n'); }
                catch (Exception) { skipped++; continue; }
                Assert.True(Legacy(text) == TextDirection.Detect(text, html: true), path);
                checkedBodies++;
            }
        }

        Assert.True(checkedBodies >= 300, $"checked only {checkedBodies}");
        Assert.True(skipped <= 40, $"skipped {skipped} unparsable bodies, checked {checkedBodies}");
    }

    [Fact]
    public void HtmlWithoutMarkupDirectionAllocatesNothing()
    {
        var html = "<html><body><p>" + new string('x', 1000) + "</p><b>א</b></body></html>";
        Assert.Equal("ltr", TextDirection.Detect(html, html: true));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            TextDirection.Detect(html, html: true);
        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, after - before);
    }

    // Verbatim copy of the pre-optimization path (StripTags + FromFirstStrong).
    private static string Legacy(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "ltr";
        var m = LegacyHtmlDir().Match(html);
        if (m.Success) return Norm(m.Groups[1].Value);
        m = LegacyBodyDir().Match(html);
        if (m.Success) return Norm(m.Groups[1].Value);
        m = LegacyCss().Match(html);
        if (m.Success) return Norm(m.Groups[1].Value);

        var sb = new StringBuilder(html.Length);
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>' && inTag) { inTag = false; sb.Append(' '); continue; }
            if (!inTag) sb.Append(ch);
        }

        foreach (var rune in sb.ToString().EnumerateRunes())
        {
            var v = rune.Value;
            if (v is 0x200F or 0x202B or 0x202E or 0x2067) return "rtl";
            if (v is 0x200E or 0x202A or 0x202D or 0x2066) return "ltr";
            if (v is 0x202C or 0x2068 or 0x2069) continue;
            if (v is (>= 0x0590 and <= 0x08FF) or (>= 0xFB1D and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF)
                or (>= 0x10800 and <= 0x10FFF) or (>= 0x1E800 and <= 0x1EFFF))
                return "rtl";
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
                or UnicodeCategory.LetterNumber)
                return "ltr";
        }

        return "ltr";
    }

    private static string Norm(string v) => v.Equals("rtl", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr";

    [GeneratedRegex(@"<html\b[^>]*\sdir\s*=\s*[""']?(rtl|ltr)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyHtmlDir();

    [GeneratedRegex(@"<body\b[^>]*\sdir\s*=\s*[""']?(rtl|ltr)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyBodyDir();

    [GeneratedRegex(@"direction\s*:\s*(rtl|ltr)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyCss();
}
