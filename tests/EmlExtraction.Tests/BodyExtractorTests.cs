using MimeKit;

namespace EmlExtraction.Tests;

/// <summary>Body and attachments stage bodies (<see cref="BodyExtractor"/>) over an in-memory <see cref="MimeMessage"/>.</summary>
public sealed class BodyExtractorTests
{
    [Fact]
    public void ExtractsPlainBodyAndDirections()
    {
        var message = SampleFactory.Plain("שלום", "hello\r\n");
        var warnings = new List<string>();

        var body = new BodyExtractor().ExtractBody(message, message.Subject, warnings);

        Assert.NotNull(body.Part);
        Assert.Equal("hello", body.Content);
        Assert.Equal("text/plain", body.ContentType);
        Assert.Equal(TextDirection.Rtl, body.SubjectDirection);
        Assert.Equal(TextDirection.Ltr, body.ContentDirection);
        Assert.Empty(warnings);
    }

    [Fact]
    public void HtmlBodyContentDirectionUsesHtmlDir()
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new TextPart("html") { Text = "<html dir=\"rtl\"><body>hello</body></html>" };

        var body = new BodyExtractor().ExtractBody(message, "s", []);

        Assert.Equal("text/html", body.ContentType);
        Assert.Equal(TextDirection.Rtl, body.ContentDirection);
    }

    [Fact]
    public void BodyDecodeContentFailureIsWarning()
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new TextPart("plain") { Content = new MimeContent(new ThrowingStream(new IOException("unreadable"))) };
        var warnings = new List<string>();

        var body = new BodyExtractor().ExtractBody(message, "s", warnings);

        Assert.NotNull(body.Part);
        Assert.Null(body.Content);
        Assert.Equal("body_decode_failed: unreadable", Assert.Single(warnings));
    }

    [Fact]
    public void BodyDecodeBugPropagates()
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new TextPart("plain") { Content = new MimeContent(new ThrowingStream(new InvalidCastException("boom"))) };

        Assert.Throws<InvalidCastException>(() => new BodyExtractor().ExtractBody(message, "s", []));
    }

    [Fact]
    public void NoTextPartGivesNullContent()
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new MimePart("application", "octet-stream") { FileName = "a.bin", Content = new MimeContent(new MemoryStream([1, 2])) };

        var body = new BodyExtractor().ExtractBody(message, "s", []);

        Assert.Null(body.Part);
        Assert.Null(body.Content);
        Assert.Null(body.ContentType);
    }

    [Fact]
    public void ExtractsAttachmentsExcludingBody()
    {
        var bytes = SampleFactory.WithAttachment("a.png", SampleFactory.MinimalPng(), "image", "png");
        using var input = MimeMessageParser.Open(bytes);
        var message = new MimeMessageParser().Parse(input, bytes);
        var warnings = new List<string>();

        var body = new BodyExtractor().ExtractBody(message, message.Subject, warnings);
        var attachments = new BodyExtractor().ExtractAttachments(message, body.Part, warnings);

        Assert.Equal("text/html", body.ContentType);
        var attachment = Assert.Single(attachments);
        Assert.Equal("a.png", attachment.FileName);
        Assert.Equal(SampleFactory.MinimalPng(), attachment.BinaryData);
        Assert.Empty(warnings);
    }

    private sealed class ThrowingStream(Exception ex) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw ex;

        public override int Read(Span<byte> buffer) => throw ex;
    }
}
