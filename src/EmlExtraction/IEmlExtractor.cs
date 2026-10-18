namespace EmlExtraction;

/// <summary>Extracts metadata, main content and attachments from a mail message.</summary>
public interface IEmlExtractor
{
    /// <summary>Parses <paramref name="eml"/> and returns the extraction result. Parse and decode failures are reported in <see cref="EmlExtractionResult.Warnings"/>.</summary>
    /// <param name="eml">Raw message bytes.</param>
    EmlExtractionResult Extract(ReadOnlyMemory<byte> eml);
}
