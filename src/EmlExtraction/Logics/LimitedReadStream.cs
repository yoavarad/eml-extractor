using System.Diagnostics.CodeAnalysis;
using EmlExtraction.Exceptions;

namespace EmlExtraction.Logics;

/// <summary>
/// Read-only, forward-only view of a caller's stream that counts the bytes read and throws
/// <see cref="EmlInputTooLargeException"/> once more than the limit is read (ADR-024). Reads are capped so at most
/// limit + 1 bytes are taken from the inner stream. Never seeks or disposes the inner stream.
/// </summary>
internal sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
{
    /// <summary>Bytes read from the inner stream so far.</summary>
    public long BytesRead { get; private set; }

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

    [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "ParamName names the caller's Extract(request) argument, matching the bytes path (ADR-024); it is not a parameter of Read.")]
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return 0;
        var allowed = maxBytes + 1 - BytesRead;
        if (allowed < buffer.Length)
            buffer = buffer[..(int)allowed];
        var read = inner.Read(buffer);
        BytesRead += read;
        if (BytesRead > maxBytes)
            throw new EmlInputTooLargeException(BytesRead, maxBytes, "request");
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
