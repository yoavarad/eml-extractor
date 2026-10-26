using System.Globalization;
using System.Text;
using EmlExtraction.Abstractions;
using MimeKit;

namespace EmlExtraction.Tests;

public sealed class ExoticLanguageTests
{
    private readonly CheckedExtractor _extractor = new();

    [Theory]
    [InlineData("עברית", "שלום עולם")]
    [InlineData("العربية", "السلام عليكم")]
    [InlineData("中文", "你好世界")]
    [InlineData("日本語", "こんにちは")]
    [InlineData("हिन्दी", "नमस्ते दुनिया")]
    [InlineData("ไทย", "สวัสดี")]
    [InlineData("Русский", "Привет мир")]
    [InlineData("emoji", "👨‍👩‍👧‍👦 😀🎉")]
    public void SubjectAndBodySurviveRoundTrip(string subject, string body)
    {
        var result = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain(subject, body)));
        Assert.Equal(subject, result.Metadata["subject"]);
        Assert.Equal(body, result.MainContent);
    }

    [Fact]
    public void PlainHebrewIsRtlAndEnglishIsLtr()
    {
        var rtl = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("שלום עולם", "גוף המייל")));
        Assert.Equal("rtl", rtl.SubjectDirection);
        Assert.Equal("rtl", rtl.ContentDirection);
        Assert.False(rtl.Metadata.ContainsKey("subject_direction"));
        Assert.False(rtl.Metadata.ContainsKey("content_direction"));
        Assert.Equal("שלום עולם", rtl.Metadata["subject"]);
        Assert.Equal("גוף המייל", rtl.MainContent);

        var arabic = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("مرحبا", "السلام عليكم")));
        Assert.Equal("rtl", arabic.SubjectDirection);
        Assert.Equal("rtl", arabic.ContentDirection);

        var ltr = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("Hello world", "English body")));
        Assert.Equal("ltr", ltr.SubjectDirection);
        Assert.Equal("ltr", ltr.ContentDirection);
    }

    [Fact]
    public void HtmlDirAttributeWinsOverLetterScript()
    {
        var rtlHtml = """
            <html dir="rtl"><body dir="rtl"><p>שלום</p></body></html>
            """;
        var rtl = _extractor.Extract(SampleFactory.ToBytes(Html("עברית", rtlHtml)));
        Assert.Equal("rtl", rtl.ContentDirection);
        Assert.Contains("dir=\"rtl\"", rtl.MainContent);
        Assert.Contains("שלום", rtl.MainContent);

        var ltrWrap = """
            <html dir="ltr"><body dir="ltr"><p>שלום עולם</p></body></html>
            """;
        var forced = _extractor.Extract(SampleFactory.ToBytes(Html("עברית", ltrWrap)));
        Assert.Equal("rtl", forced.SubjectDirection);
        Assert.Equal("ltr", forced.ContentDirection);
        Assert.Contains("dir=\"ltr\"", forced.MainContent);
        Assert.Contains("שלום", forced.MainContent);
    }

    [Fact]
    public void CssDirectionRtlIsDetected()
    {
        var html = """
            <html><body style="direction: rtl"><p>Hello from Outlook</p></body></html>
            """;
        var result = _extractor.Extract(SampleFactory.ToBytes(Html("hi", html)));
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("rtl", result.ContentDirection);
    }

    [Fact]
    public void BidiMarksSurviveAndSetDirection()
    {
        var rlm = "\u200F";
        var lrm = "\u200E";
        var subject = rlm + "Invoice 12";
        var body = lrm + "שלום";
        var result = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain(subject, body)));
        Assert.Contains(rlm, result.Metadata["subject"]);
        Assert.Contains(lrm, result.MainContent);
        Assert.Equal("rtl", result.SubjectDirection);
        Assert.Equal("ltr", result.ContentDirection);
    }

    [Fact]
    public void FirstStrongLetterWinsInMixedSubject()
    {
        var hebrewFirst = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("עברית + English", "bidi body")));
        Assert.Equal("rtl", hebrewFirst.SubjectDirection);
        Assert.Equal("ltr", hebrewFirst.ContentDirection);

        var englishFirst = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("mix שלום 中文", "mix body")));
        Assert.Equal("ltr", englishFirst.SubjectDirection);
        Assert.Equal("ltr", englishFirst.ContentDirection);
    }

    public static TheoryData<string> Sequences => new()
    {
        "\U0001F468‍\U0001F469‍\U0001F467",
        "\U0001F44D\U0001F3FD",
        "\U0001F1EE\U0001F1F1",
        "1️⃣",
        "é",
        "é",
        "שָׁלוֹם",
    };

    [Theory]
    [MemberData(nameof(Sequences))]
    public void SubjectBEncodedWordIsOrdinalExact(string seq)
    {
        var r = _extractor.Extract(Eml("Subject: =?UTF-8?B?" + B64(seq) + "?=", "text/plain; charset=utf-8", "7bit", "x"));
        Assert.Equal(seq, r.Metadata["subject"]);
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void SubjectQEncodedWordIsOrdinalExact(string seq)
    {
        var r = _extractor.Extract(Eml("Subject: =?UTF-8?Q?" + Qp(seq) + "?=", "text/plain; charset=utf-8", "7bit", "x"));
        Assert.Equal(seq, r.Metadata["subject"]);
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void PlainBodyIsOrdinalExactInAllTransferEncodings(string seq)
    {
        var text = "a " + seq + " b";
        foreach (var (cte, body) in new[]
                 {
                     ("base64", B64(text)),
                     ("quoted-printable", Qp(text)),
                     ("8bit", text),
                 })
        {
            var r = _extractor.Extract(Eml("Subject: s", "text/plain; charset=utf-8", cte, body));
            Assert.Equal(text, r.MainContent);
        }
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void HtmlBodyIsOrdinalExactInAllTransferEncodings(string seq)
    {
        var html = "<html><body><p>a " + seq + " b</p></body></html>";
        foreach (var (cte, body) in new[]
                 {
                     ("base64", B64(html)),
                     ("quoted-printable", Qp(html)),
                     ("8bit", html),
                 })
        {
            var r = _extractor.Extract(Eml("Subject: s", "text/html; charset=utf-8", cte, body));
            Assert.Contains("a " + seq + " b", r.MainContent, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void AttachmentFileNameIsOrdinalExact(string seq)
    {
        var name = seq + ".txt";
        var encodedWord = "=?UTF-8?B?" + B64(name) + "?=";
        var rfc2231 = "UTF-8''" + string.Concat(Encoding.UTF8.GetBytes(name).Select(b => "%" + b.ToString("X2", CultureInfo.InvariantCulture)));
        foreach (var disposition in new[]
                 {
                     "attachment; filename=\"" + encodedWord + "\"",
                     "attachment; filename*=" + rfc2231,
                 })
        {
            var att = Assert.Single(_extractor.Extract(WithFile(disposition)).Attachments);
            Assert.Equal(name, att.FileName);
        }
    }

    [Fact]
    public void ZwjSequenceSplitAcrossAdjacentEncodedWordsIsOrdinalExact()
    {
        const string first = "\U0001F468‍";
        const string second = "\U0001F469‍\U0001F467";
        foreach (var (word1, word2) in new[]
                 {
                     ("=?UTF-8?B?" + B64(first) + "?=", "=?UTF-8?B?" + B64(second) + "?="),
                     ("=?UTF-8?Q?" + Qp(first) + "?=", "=?UTF-8?Q?" + Qp(second) + "?="),
                 })
        {
            var r = _extractor.Extract(Eml("Subject: " + word1 + "\r\n " + word2, "text/plain; charset=utf-8", "7bit", "x"));
            Assert.Equal(first + second, r.Metadata["subject"]);
        }
    }

    [Fact]
    public void Rfc2231FlagFilenameIsOrdinalExact()
    {
        var att = Assert.Single(_extractor.Extract(
            WithFile("attachment; filename*=UTF-8''%F0%9F%87%AE%F0%9F%87%B1.txt")).Attachments);
        Assert.Equal("\U0001F1EE\U0001F1F1.txt", att.FileName);
    }

    private static byte[] Eml(string subjectHeader, string contentType, string cte, string body) =>
        Encoding.UTF8.GetBytes("MIME-Version: 1.0\r\n" + subjectHeader + "\r\nContent-Type: " + contentType
                               + "\r\nContent-Transfer-Encoding: " + cte + "\r\n\r\n" + body + "\r\n");

    private static byte[] WithFile(string disposition) =>
        Encoding.UTF8.GetBytes("MIME-Version: 1.0\r\nSubject: s\r\nContent-Type: multipart/mixed; boundary=\"b\"\r\n\r\n"
                               + "--b\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: 7bit\r\n\r\nbody\r\n"
                               + "--b\r\nContent-Type: application/octet-stream\r\nContent-Transfer-Encoding: base64\r\n"
                               + "Content-Disposition: " + disposition + "\r\n\r\n" + B64("data") + "\r\n--b--\r\n");

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    // Every byte outside [A-Za-z0-9] as =XX (valid for QP bodies and RFC 2047 Q words).
    private static string Qp(string s) => string.Concat(Encoding.UTF8.GetBytes(s)
        .Select(b => char.IsAsciiLetterOrDigit((char)b) ? ((char)b).ToString() : "=" + b.ToString("X2", CultureInfo.InvariantCulture)));

    private static MimeMessage Html(string subject, string html)
    {
        var message = SampleFactory.Plain(subject, "unused");
        message.Body = new TextPart("html") { Text = html };
        return message;
    }
}
