using EmlExtraction.Exceptions;
using EmlExtraction.Logics;
using MimeKit;
using MimeKit.Tnef;

namespace EmlExtraction.Tests;

/// <summary>
/// ADR-023 per-part catch sites: a content exception (one of the seven content types) stays its warning with a partial
/// result; any other exception is wrapped as <see cref="EmlExtractionException"/> by the enclosing stage.
/// </summary>
public sealed class ContentCatchSiteTests
{
    private readonly EmlExtractor _extractor = new();

    [Fact]
    public void PartDecodeContentFailureIsWarning()
    {
        var result = _extractor.Extract(WithAttachment(Throwing(new IOException("unreadable"))));

        Assert.StartsWith("part_decode_failed: bad.bin: unreadable", Assert.Single(result.Warnings), StringComparison.Ordinal);
        Assert.Equal("hi", result.MainContent);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public void PartDecodeBugIsWrapped()
    {
        var boom = new InvalidCastException("boom");
        AssertWrapped("attachments", boom, WithAttachment(Throwing(boom)));
    }

    [Fact]
    public void BodyDecodeContentFailureIsWarning()
    {
        var result = _extractor.Extract(WithBody(Throwing(new System.Text.DecoderFallbackException("bad charset"))));

        Assert.StartsWith("body_decode_failed:", Assert.Single(result.Warnings), StringComparison.Ordinal);
        Assert.Null(result.MainContent);
    }

    [Fact]
    public void BodyDecodeBugIsWrapped()
    {
        var boom = new InvalidCastException("boom");
        AssertWrapped("body", boom, WithBody(Throwing(boom)));
    }

    [Fact]
    public void Rfc822ContentFailureIsWarning()
    {
        var result = _extractor.Extract(WithNested(Throwing(new FormatException("broken"))));

        Assert.StartsWith("rfc822_extract_failed: broken", Assert.Single(result.Warnings), StringComparison.Ordinal);
        Assert.Equal("hi", result.MainContent);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public void Rfc822BugIsWrapped()
    {
        var boom = new InvalidCastException("boom");
        AssertWrapped("attachments", boom, WithNested(Throwing(boom)));
    }

    [Fact]
    public void TnefContentFailureIsWarning()
    {
        var result = _extractor.Extract(WithTnef(Throwing(new IOException("truncated"))));

        Assert.Equal(2, result.Warnings.Count);
        Assert.StartsWith("tnef_extract_failed: truncated", result.Warnings[0], StringComparison.Ordinal);
        Assert.StartsWith("part_decode_failed:", result.Warnings[1], StringComparison.Ordinal);
        Assert.Equal("hi", result.MainContent);
    }

    [Fact]
    public void TnefBugIsWrapped()
    {
        var boom = new InvalidCastException("boom");
        AssertWrapped("attachments", boom, WithTnef(Throwing(boom)));
    }

    private void AssertWrapped(string stage, Exception inner, MimeMessage message)
    {
        var ex = Assert.Throws<EmlExtractionException>(() => _extractor.Extract(message));
        Assert.Equal(stage, ex.Stage);
        Assert.Same(inner, ex.InnerException);
    }

    private static MimeContent Throwing(Exception ex) => new(new ThrowingStream(ex));

    private static MimeMessage WithAttachment(MimeContent content)
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "hi" },
            new MimePart("application", "octet-stream") { FileName = "bad.bin", Content = content }
        };
        return message;
    }

    private static MimeMessage WithBody(MimeContent content)
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new TextPart("plain") { Content = content };
        return message;
    }

    private static MimeMessage WithNested(MimeContent content)
    {
        var inner = SampleFactory.Plain("inner", "t");
        inner.Body = new MimePart("application", "octet-stream") { FileName = "x.bin", Content = content };
        var message = SampleFactory.Plain("s", "t");
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "hi" },
            new MessagePart { Message = inner }
        };
        return message;
    }

    private static MimeMessage WithTnef(MimeContent content)
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "hi" },
            new TnefPart { FileName = "winmail.dat", Content = content }
        };
        return message;
    }

    private sealed class ThrowingStream(Exception ex) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 10;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw ex;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
