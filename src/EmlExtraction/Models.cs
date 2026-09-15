namespace EmlExtraction;

public interface IEmlDetector
{
    bool IsEml(ReadOnlySpan<byte> data);
}

public interface IEmlExtractor
{
    EmlExtractionResult Extract(ReadOnlyMemory<byte> eml);
}

public sealed record EmlExtractionResult(
    IReadOnlyDictionary<string, string> Metadata,
    string XFields,
    string? MainContent,
    string? MainContentMimeType,
    IReadOnlyList<ExtractedAttachment> Attachments,
    IReadOnlyList<string> Warnings);

public sealed record ExtractedAttachment(
    string Name,
    long Size,
    string? FullPath,
    byte[] Data,
    string? ContentId,
    string? Disposition);
