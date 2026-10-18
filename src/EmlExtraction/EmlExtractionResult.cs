namespace EmlExtraction;

/// <summary>Immutable result of one extraction.</summary>
/// <param name="Metadata">Header map (from, to, cc, bcc, subject, date and related keys).</param>
/// <param name="XFields">Every X- / X_ header line, <c>Field: value</c>, joined by <c>\n</c> in header order.</param>
/// <param name="MainContent">Decoded text of the selected body part; HTML is kept as markup. <see langword="null"/> when no body is selectable.</param>
/// <param name="MainContentMimeType">Media type of <paramref name="MainContent"/>, or <see langword="null"/> when it is <see langword="null"/>.</param>
/// <param name="Attachments">Collected parts in MIME tree order.</param>
/// <param name="Warnings">Non-fatal diagnostics recorded during extraction.</param>
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
