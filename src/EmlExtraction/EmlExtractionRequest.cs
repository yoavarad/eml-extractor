namespace EmlExtraction;

/// <summary>Input of <see cref="EmlExtractor.Extract(EmlExtractionRequest)"/>. Set exactly one of <see cref="FileBytes"/> and <see cref="InputStream"/>.</summary>
public sealed record EmlExtractionRequest
{
    private readonly ReadOnlyMemory<byte> _fileBytes;

    /// <summary>Whole message bytes. A <c>byte[]</c> converts implicitly. Setting it, even to empty, counts as set.</summary>
    public ReadOnlyMemory<byte> FileBytes
    {
        get => _fileBytes;
        init
        {
            _fileBytes = value;
            HasFileBytes = true;
        }
    }

    /// <summary>
    /// Message stream, read forward from its current position until it ends (ADR-024). It need not be seekable. The
    /// library never seeks or disposes it; the caller owns it.
    /// </summary>
    public Stream? InputStream { get; init; }

    /// <summary>Caller's name for the file, for traceability only. Never changes the result.</summary>
    public string? FileName { get; init; }

    /// <summary>Whether <see cref="FileBytes"/> was set.</summary>
    internal bool HasFileBytes { get; private init; }
}
