using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EmlExtraction;

/// <summary>
/// Paragraph direction for extracted subject/body: HTML <c>dir</c> / CSS <c>direction</c>,
/// else Unicode first-strong (UAX #9). Values are <c>ltr</c> or <c>rtl</c>.
/// </summary>
internal static partial class TextDirection
{
    public const string Ltr = "ltr";
    public const string Rtl = "rtl";

    public static string Detect(string? text, bool html)
    {
        if (string.IsNullOrEmpty(text))
            return Ltr;

        if (html)
        {
            var fromMarkup = FromHtml(text);
            if (fromMarkup is not null)
                return fromMarkup;
            return FromFirstStrong(StripTags(text));
        }

        return FromFirstStrong(text);
    }

    private static string? FromHtml(string html)
    {
        var htmlTag = HtmlDir().Match(html);
        if (htmlTag.Success)
            return Normalize(htmlTag.Groups[1].Value);

        var bodyTag = BodyDir().Match(html);
        if (bodyTag.Success)
            return Normalize(bodyTag.Groups[1].Value);

        var css = CssDirection().Match(html);
        if (css.Success)
            return Normalize(css.Groups[1].Value);

        return null;
    }

    private static string FromFirstStrong(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var v = rune.Value;
            if (v is 0x200F or 0x202B or 0x202E or 0x2067)
                return Rtl;
            if (v is 0x200E or 0x202A or 0x202D or 0x2066)
                return Ltr;
            if (v is 0x202C or 0x2068 or 0x2069)
                continue;
            if (IsRtlLetter(v))
                return Rtl;
            if (IsLtrLetter(rune))
                return Ltr;
        }

        return Ltr;
    }

    private static bool IsRtlLetter(int v)
        => v is (>= 0x0590 and <= 0x08FF)
            or (>= 0xFB1D and <= 0xFDFF)
            or (>= 0xFE70 and <= 0xFEFF)
            or (>= 0x10800 and <= 0x10FFF)
            or (>= 0x1E800 and <= 0x1EFFF);

    private static bool IsLtrLetter(Rune rune)
    {
        var cat = Rune.GetUnicodeCategory(rune);
        return cat is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber;
    }

    private static string StripTags(string html)
    {
        var sb = new StringBuilder(html.Length);
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>' && inTag)
            {
                inTag = false;
                sb.Append(' ');
                continue;
            }

            if (!inTag)
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string Normalize(string value)
        => value.Equals("rtl", StringComparison.OrdinalIgnoreCase) ? Rtl : Ltr;

    [GeneratedRegex(@"<html\b[^>]*\sdir\s*=\s*[""']?(rtl|ltr)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlDir();

    [GeneratedRegex(@"<body\b[^>]*\sdir\s*=\s*[""']?(rtl|ltr)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BodyDir();

    [GeneratedRegex(@"direction\s*:\s*(rtl|ltr)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssDirection();
}
