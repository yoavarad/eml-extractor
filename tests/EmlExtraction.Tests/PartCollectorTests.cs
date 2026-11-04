using System.Diagnostics.CodeAnalysis;
using EmlExtraction.Logics;
using MimeKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-026 per-part isolation in <see cref="PartCollector"/>: a bug in one part skips only that part.</summary>
public sealed class PartCollectorTests
{
    [Fact]
    public void BugInOnePartSkipsOnlyThatPart()
    {
        var root = new Multipart("mixed") { Part("bad.bin", new ThrowingStream(new InvalidCastException("SECRET"))), Part("good.bin", new MemoryStream([1, 2])) };
        var warnings = new List<string>();

        var parts = PartCollector.Collect(root, body: null, warnings);

        Assert.Equal("good.bin", Assert.Single(parts).FileName);
        Assert.Equal("internal_error: attachments: InvalidCastException", Assert.Single(warnings));
    }

    [Fact]
    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "The test injects a runtime-reserved OutOfMemoryException to prove it propagates.")]
    public void OutOfMemoryInAPartPropagates()
    {
        var oom = new OutOfMemoryException("oom");
        var root = new Multipart("mixed") { Part("bad.bin", new ThrowingStream(oom)) };

        Assert.Same(oom, Assert.Throws<OutOfMemoryException>(() => PartCollector.Collect(root, body: null, [])));
    }

    private static MimePart Part(string name, Stream content)
        => new("application", "octet-stream") { FileName = name, Content = new MimeContent(content) };

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
