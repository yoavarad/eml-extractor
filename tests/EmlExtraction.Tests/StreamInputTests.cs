using System.Text;
using EmlExtraction.Exceptions;
using EmlExtraction.Logics;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>Stream input (ADR-024): Extract(Stream) and EmlExtractionRequest.InputStream.</summary>
public sealed class StreamInputTests
{
    private const string Eml = "From: Alice <alice@example.com>\r\nTo: Bob <bob@example.com>\r\nSubject: hello\r\n\r\nbody text\r\n";
    private const string MboxFrom = "From alice@example.com Mon Sep 15 07:00:00 2026\r\n";

    private readonly CheckedExtractor _extractor = new();

    [Fact]
    public void StreamPathsEqualBytesPathOnEveryGoldenInput()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "corpus", "golden")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var inputs = Directory.EnumerateFiles(Path.Combine(dir.FullName, "corpus", "golden"), "input.eml",
            SearchOption.AllDirectories).ToList();
        Assert.True(inputs.Count >= 1212, $"expected seed + bulk inputs, found {inputs.Count}");
        foreach (var path in inputs)
        {
            var bytes = File.ReadAllBytes(path);
            var expected = ResultProjection.Of(_extractor.Extract(bytes));

            using var seekable = new MemoryStream(bytes, writable: false);
            Assert.True(expected.SequenceEqual(ResultProjection.Of(_extractor.Extract(seekable))), $"seekable {path}");

            using var nonSeekable = new NonSeekableStream(bytes);
            Assert.True(expected.SequenceEqual(ResultProjection.Of(_extractor.Extract(nonSeekable))), $"non-seekable {path}");

            using var viaRequest = new NonSeekableStream(bytes);
            var result = _extractor.Extract(new EmlExtractionRequest { InputStream = viaRequest, FileName = path });
            Assert.True(expected.SequenceEqual(ResultProjection.Of(result)), $"request {path}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MboxFromLineWorksFromNonSeekableStream(bool bom)
    {
        byte[] bytes = [.. bom ? new byte[] { 0xEF, 0xBB, 0xBF } : [], .. Encoding.ASCII.GetBytes(MboxFrom + Eml)];
        using var stream = new NonSeekableStream(bytes);

        var result = _extractor.Extract(stream);

        Assert.Empty(result.Warnings);
        Assert.Equal("hello", result.Metadata[MetadataKeys.Subject]);
        Assert.Contains("alice@example.com", result.Metadata[MetadataKeys.From]);
        Assert.Equal(ResultProjection.Of(_extractor.Extract(bytes)), ResultProjection.Of(result));
    }

    [Fact]
    public void MboxTailCountsAgainstLimit()
    {
        var bytes = Encoding.ASCII.GetBytes(MboxFrom + Eml + MboxFrom + Eml + new string('x', 5000));
        using var stream = new NonSeekableStream(bytes);

        var ex = Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(4096).Extract(stream));

        Assert.Equal(4097, ex.InputLength);
        Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(4096).Extract(bytes));
    }

    [Fact]
    public void ShortStreamUnderPeekLengthIsParseResultNotError()
    {
        using var stream = new NonSeekableStream("Fro"u8.ToArray());

        var viaStream = _extractor.Extract(stream);

        Assert.Equal(ResultProjection.Of(_extractor.Extract("Fro"u8.ToArray())), ResultProjection.Of(viaStream));
    }

    [Fact]
    public void AtLimitIsParsed()
    {
        using var stream = new NonSeekableStream(Message(1024));

        var result = new EmlExtractor(1024).Extract(stream);

        Assert.Empty(result.Warnings);
        Assert.Equal("a@b.com", result.Metadata[MetadataKeys.From]);
    }

    [Fact]
    public void OverLimitThrowsWithBytesReadAndNoWarning()
    {
        using var stream = new NonSeekableStream(Message(1025));

        var ex = Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(1024).Extract(stream));

        Assert.Equal(1025, ex.InputLength);
        Assert.Equal(1024, ex.MaxInputBytes);
        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    public void OverLimitViaRequestThrows()
    {
        using var stream = new NonSeekableStream(Message(1025));

        Assert.Throws<EmlInputTooLargeException>(
            () => new EmlExtractor(1024).Extract(new EmlExtractionRequest { InputStream = stream }));
    }

    [Fact]
    public void ReadingStopsNearLimit()
    {
        using var stream = new NonSeekableStream(Message(10_000_000));

        var ex = Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(4096).Extract(stream));

        Assert.Equal(4097, ex.InputLength);
        Assert.Equal(4097, stream.BytesRead);
    }

    [Fact]
    public void ReadsFromCurrentPositionWithoutSeekingOrDisposing()
    {
        var eml = Encoding.ASCII.GetBytes(Eml);
        var stream = new TrackingMemoryStream([.. "junk"u8, .. eml]) { Position = 4 };

        var result = _extractor.Extract(stream);

        Assert.Equal("hello", result.Metadata[MetadataKeys.Subject]);
        Assert.False(stream.Disposed);
        Assert.False(stream.Seeked);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void NonSeekableStreamIsNotDisposed()
    {
        var stream = new NonSeekableStream(Encoding.ASCII.GetBytes(Eml));

        _extractor.Extract(new EmlExtractionRequest { InputStream = stream });
        Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(10).Extract(new NonSeekableStream(Message(100))));

        Assert.False(stream.Disposed);
    }

    [Fact]
    public void OverLimitStreamIsNotDisposed()
    {
        var stream = new NonSeekableStream(Message(100));

        Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(10).Extract(stream));

        Assert.False(stream.Disposed);
    }

    [Fact]
    public void NullStreamThrowsArgumentNull()
        => Assert.Throws<ArgumentNullException>(() => new EmlExtractor().Extract((Stream)null!));

    [Fact]
    public void NeitherInputSetThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new EmlExtractor().Extract(new EmlExtractionRequest()));

        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    public void BothInputsSetThrowsArgumentException()
    {
        var bytes = Encoding.ASCII.GetBytes(Eml);
        using var stream = new MemoryStream(bytes);

        var ex = Assert.Throws<ArgumentException>(
            () => new EmlExtractor().Extract(new EmlExtractionRequest { FileBytes = bytes, InputStream = stream }));

        Assert.Equal("request", ex.ParamName);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void EmptyFileBytesStillCountsAsSet()
    {
        var result = new EmlExtractor().Extract(new EmlExtractionRequest { FileBytes = ReadOnlyMemory<byte>.Empty });

        Assert.Equal(ResultProjection.Of(new EmlExtractor().Extract(ReadOnlyMemory<byte>.Empty)), ResultProjection.Of(result));
    }

    [Fact]
    public void ReaderIOExceptionIsParseFailedWarning()
    {
        var result = new EmlExtractor().Extract(new FailingStream());

        Assert.StartsWith("parse_failed: IOException", Assert.Single(result.Warnings));
    }

    private static byte[] Message(int length)
    {
        var head = "From: a@b.com\r\nSubject: hi\r\nContent-Type: text/plain\r\n\r\n";
        return Encoding.ASCII.GetBytes(head + new string('x', length - head.Length));
    }

    /// <summary>Forward-only stream over bytes: CanSeek false, Length/Position/Seek throw. Tracks bytes read and disposal.</summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private int _position;

        public bool Disposed { get; private set; }

        public long BytesRead => _position;

        public override bool CanRead => !Disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            var n = Math.Min(count, data.Length - _position);
            Array.Copy(data, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingMemoryStream(byte[] data) : MemoryStream(data, writable: false)
    {
        public bool Disposed { get; private set; }

        public bool Seeked { get; private set; }

        public override long Seek(long offset, SeekOrigin loc)
        {
            Seeked = true;
            return base.Seek(offset, loc);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("disk gone");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
