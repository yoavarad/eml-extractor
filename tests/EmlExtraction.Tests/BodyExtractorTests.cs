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

        var body = BodyExtractor.ExtractBody(message, message.Subject, warnings);

        Assert.NotNull(body.Part);
        Assert.Equal("hello", body.Content);
        Assert.Equal("text/plain", body.ContentType);
        Assert.Equal(TextDirection.Rtl, body.SubjectDirection);
        Assert.Equal(TextDirection.Ltr, body.ContentDirection);
        Assert.Empty(warnings);
    }

    [Fact]
    public void NoTextPartGivesNullContent()
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new MimePart("application", "octet-stream") { FileName = "a.bin", Content = new MimeContent(new MemoryStream([1, 2])) };

        var body = BodyExtractor.ExtractBody(message, "s", []);

        Assert.Null(body.Part);
        Assert.Null(body.Content);
        Assert.Null(body.ContentType);
    }

    [Fact]
    public void ExtractsAttachmentsExcludingBody()
    {
        var bytes = SampleFactory.WithAttachment("a.png", SampleFactory.MinimalPng(), "image", "png");
        using var input = MimeMessageParser.Open(bytes);
        var message = MimeMessageParser.Parse(input, bytes);
        var warnings = new List<string>();

        var body = BodyExtractor.ExtractBody(message, message.Subject, warnings);
        var attachments = BodyExtractor.ExtractAttachments(message, body.Part, warnings);

        Assert.Equal("text/html", body.ContentType);
        var attachment = Assert.Single(attachments);
        Assert.Equal("a.png", attachment.FileName);
        Assert.Equal(SampleFactory.MinimalPng(), attachment.BinaryData);
        Assert.Empty(warnings);
    }
}
