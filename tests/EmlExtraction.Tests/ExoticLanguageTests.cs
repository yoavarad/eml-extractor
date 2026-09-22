using MimeKit;

namespace EmlExtraction.Tests;

public sealed class ExoticLanguageTests
{
    private readonly EmlExtractor _extractor = new();

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
        Assert.Equal("rtl", rtl.Metadata["subject_direction"]);
        Assert.Equal("rtl", rtl.Metadata["content_direction"]);
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

    private static MimeMessage Html(string subject, string html)
    {
        var message = SampleFactory.Plain(subject, "unused");
        message.Body = new TextPart("html") { Text = html };
        return message;
    }
}
