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
}
