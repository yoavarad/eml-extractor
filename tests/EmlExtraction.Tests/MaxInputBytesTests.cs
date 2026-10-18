using System.Text;

namespace EmlExtraction.Tests;

/// <summary>maxInputBytes limit (ydk:req:extraction/max-input-size).</summary>
public sealed class MaxInputBytesTests
{
    private static byte[] Message(int length)
    {
        var head = "From: a@b.com\r\nSubject: hi\r\nContent-Type: text/plain\r\n\r\n";
        var body = new string('x', length - head.Length);
        return Encoding.ASCII.GetBytes(head + body);
    }

    [Fact]
    public void OverLimitReturnsOnlyInputTooLargeWarning()
    {
        var result = new EmlExtractor(1024).Extract(Message(1025));

        Assert.Equal("input_too_large: 1025 > 1024", Assert.Single(result.Warnings));
        Assert.Equal(["bcc", "cc", "from", "to", "xfields"], result.Metadata.Keys.Order(StringComparer.Ordinal));
        Assert.All(result.Metadata.Values, v => Assert.Equal("", v));
        Assert.Equal("", result.XFields);
        Assert.Null(result.MainContent);
        Assert.Null(result.MainContentMimeType);
        Assert.Empty(result.Attachments);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("ltr", result.ContentDirection);
    }

    [Fact]
    public void AtLimitIsParsed()
    {
        var result = new EmlExtractor(1024).Extract(Message(1024));

        Assert.Empty(result.Warnings);
        Assert.Equal("a@b.com", result.Metadata["from"]);
        Assert.NotNull(result.MainContent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLimitThrows(long limit)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EmlExtractor(limit));

    [Fact]
    public void DefaultConstantIs150MiB() => Assert.Equal(157_286_400L, EmlExtractor.DefaultMaxInputBytes);

    [Fact]
    [Trait("Category", "Large")]
    public void DefaultBoundary()
    {
        var extractor = new EmlExtractor();
        var bytes = Message(checked((int)EmlExtractor.DefaultMaxInputBytes + 1));

        var over = extractor.Extract(bytes);
        Assert.StartsWith("input_too_large", Assert.Single(over.Warnings));

        var at = extractor.Extract(bytes.AsMemory(0, (int)EmlExtractor.DefaultMaxInputBytes));
        Assert.DoesNotContain(at.Warnings, w => w.StartsWith("input_too_large"));
    }
}
