namespace EmlExtraction;

/// <summary>Input of <see cref="EmlExtractor.Extract(EmlExtractionRequest)"/>.</summary>
public sealed record EmlExtractionRequest
{
    /// <summary>Whole message bytes. A <c>byte[]</c> converts implicitly.</summary>
    public required ReadOnlyMemory<byte> FileBytes { get; init; }

    /// <summary>Caller's name for the file, for traceability only. Never changes the result.</summary>
    public string? FileName { get; init; }
}
