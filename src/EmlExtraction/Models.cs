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
    IReadOnlyList<string> Warnings)
{
    /// <summary>Paragraph direction of <c>subject</c>: <c>ltr</c> or <c>rtl</c>.</summary>
    public string SubjectDirection { get; init; } = "ltr";

    /// <summary>Paragraph direction of <c>MainContent</c>: <c>ltr</c> or <c>rtl</c>.</summary>
    public string ContentDirection { get; init; } = "ltr";
}

public sealed record ExtractedAttachment(
    string Name,
    long Size,
    string? FullPath,
    byte[] Data,
    string? ContentId,
    string? Disposition);
