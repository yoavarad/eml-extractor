using EmlExtraction.Logics;
using MimeKit;
using MimeKit.Tnef;

namespace EmlExtraction.Tests;

/// <summary>
/// Per-part catch sites: a content exception (one of the seven content types) stays its warning with a partial result
/// (ADR-023); any other exception (library bug) records <c>internal_error: &lt;stage&gt;: &lt;Type&gt;</c> and skips only that
/// part, so the other parts are still returned (ADR-026).
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
    public void PartDecodeBugSkipsOnlyThatPart()
    {
        var message = WithAttachment(Throwing(new InvalidCastException("SECRET")));
        AddGoodPart(message);

        var result = _extractor.Extract(message);

        Assert.Equal("internal_error: attachments: InvalidCastException", Assert.Single(result.Warnings));
        Assert.Equal("hi", result.MainContent);
        Assert.Equal("good.bin", Assert.Single(result.Attachments).FileName);
    }

    [Fact]
    public void BodyDecodeContentFailureIsWarning()
    {
        var result = _extractor.Extract(WithBody(Throwing(new System.Text.DecoderFallbackException("bad charset"))));

        Assert.StartsWith("body_decode_failed:", Assert.Single(result.Warnings), StringComparison.Ordinal);
        Assert.Null(result.MainContent);
    }

    [Fact]
    public void BodyDecodeBugGivesNullContent()
    {
        var result = _extractor.Extract(WithBody(Throwing(new InvalidCastException("SECRET"))));

        Assert.Equal("internal_error: body: InvalidCastException", Assert.Single(result.Warnings));
        Assert.Null(result.MainContent);
        Assert.Null(result.MainContentMimeType);
        Assert.Equal("ltr", result.ContentDirection);
        Assert.Equal("s", result.Metadata["subject"]);
        Assert.Empty(result.Attachments);
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
    public void Rfc822BugSkipsOnlyThatPart()
    {
        var message = WithNested(Throwing(new InvalidCastException("SECRET")));
        AddGoodPart(message);

        var result = _extractor.Extract(message);

        Assert.Equal("internal_error: attachments: InvalidCastException", Assert.Single(result.Warnings));
        Assert.Equal("hi", result.MainContent);
        Assert.Equal("good.bin", Assert.Single(result.Attachments).FileName);
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
    public void TnefBugKeepsRawPartFallback()
    {
        // The TNEF bug is recorded, then the raw winmail.dat fallback is tried; its decode hits the same bug.
        var message = WithTnef(Throwing(new InvalidCastException("SECRET")));
        AddGoodPart(message);

        var result = _extractor.Extract(message);

        Assert.Equal(
            ["internal_error: attachments: InvalidCastException", "internal_error: attachments: InvalidCastException"],
            result.Warnings);
        Assert.Equal("hi", result.MainContent);
        Assert.Equal("good.bin", Assert.Single(result.Attachments).FileName);
    }

    [Fact]
    public void TnefBugFallsBackToRawPart()
    {
        // The bug hits only the first read (TNEF extraction); the raw winmail.dat fallback then decodes.
        var result = _extractor.Extract(WithTnef(new MimeContent(new ThrowOnceStream(new InvalidCastException("SECRET")))));

        Assert.Equal("internal_error: attachments: InvalidCastException", Assert.Single(result.Warnings));
        Assert.Equal("winmail.dat", Assert.Single(result.Attachments).FileName);
    }

    private static void AddGoodPart(MimeMessage message)
        => Assert.IsType<Multipart>(message.Body).Add(
            new MimePart("application", "octet-stream") { FileName = "good.bin", Content = new MimeContent(new MemoryStream([1, 2, 3])) });

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

    private sealed class ThrowOnceStream(Exception ex) : MemoryStream([1, 2, 3])
    {
        private bool _thrown;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_thrown)
                return base.Read(buffer, offset, count);
            _thrown = true;
            throw ex;
        }

        public override int Read(Span<byte> buffer)
        {
            if (_thrown)
                return base.Read(buffer);
            _thrown = true;
            throw ex;
        }
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
