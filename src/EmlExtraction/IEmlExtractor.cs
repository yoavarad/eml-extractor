namespace EmlExtraction;

/// <summary>Extracts metadata, main content and attachments from a mail message.</summary>
public interface IEmlExtractor
{
    /// <summary>Parses <paramref name="eml"/> and returns the extraction result. Does not throw on malformed input.</summary>
    /// <param name="eml">Raw message bytes.</param>
    EmlExtractionResult Extract(ReadOnlyMemory<byte> eml);
}
