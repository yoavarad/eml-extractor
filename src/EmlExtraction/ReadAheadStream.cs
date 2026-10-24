namespace EmlExtraction;

/// <summary>
/// Read-only, forward-only stream that reads the first bytes of an inner stream up front (so they can be inspected,
/// for the mbox "From " check) and then replays them before the rest. Works on non-seekable streams; never seeks or
/// disposes the inner stream.
/// </summary>
internal sealed class ReadAheadStream : Stream
{
    private readonly Stream _inner;
    private readonly byte[] _head;
    private readonly int _headLength;
    private int _headPosition;

    /// <summary>Reads up to <paramref name="count"/> bytes of <paramref name="inner"/> (fewer only at end of stream).</summary>
    public ReadAheadStream(Stream inner, int count)
    {
        _inner = inner;
        _head = new byte[count];
        while (_headLength < count)
        {
            var read = inner.Read(_head, _headLength, count - _headLength);
            if (read == 0)
                break;
            _headLength += read;
        }
    }

    /// <summary>The bytes read ahead; shorter than requested only if the stream ended.</summary>
    public ReadOnlySpan<byte> Head => _head.AsSpan(0, _headLength);

    public override bool CanRead => true;

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
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (_headPosition < _headLength)
        {
            var n = Math.Min(buffer.Length, _headLength - _headPosition);
            _head.AsSpan(_headPosition, n).CopyTo(buffer);
            _headPosition += n;
            return n;
        }

        return _inner.Read(buffer);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
