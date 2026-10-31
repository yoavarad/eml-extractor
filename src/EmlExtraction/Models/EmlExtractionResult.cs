namespace EmlExtraction.Models;

/// <summary>Immutable result of one extraction.</summary>
/// <param name="Metadata">Header map (from, to, cc, bcc, subject, date and related keys).</param>
/// <param name="MainContent">Decoded text of the selected body part, trailing CR/LF trimmed; HTML is kept as markup. <see langword="null"/> when no body is selectable or parsing or decoding fails.</param>
/// <param name="MainContentMimeType">Media type of <paramref name="MainContent"/>, or <see langword="null"/> when it is <see langword="null"/>.</param>
/// <param name="Attachments">Collected parts in MIME tree order.</param>
/// <param name="Warnings">Non-fatal diagnostics recorded during extraction.</param>
public sealed record EmlExtractionResult(
    IReadOnlyDictionary<string, string> Metadata,
    string? MainContent,
    string? MainContentMimeType,
    IReadOnlyList<ExtractedAttachment> Attachments,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Paragraph direction of <c>subject</c>: <c>ltr</c> or <c>rtl</c>.</summary>
    public string SubjectDirection { get; init; } = "ltr";

    /// <summary>Paragraph direction of <c>MainContent</c>: <c>ltr</c> or <c>rtl</c>.</summary>
    public string ContentDirection { get; init; } = "ltr";

    /// <summary>
    /// Mailboxes per participant header, keyed by <see cref="MetadataKeys.From"/>, <see cref="MetadataKeys.To"/>,
    /// <see cref="MetadataKeys.Cc"/>, <see cref="MetadataKeys.Bcc"/>, <see cref="MetadataKeys.ReplyTo"/> and
    /// <see cref="MetadataKeys.Sender"/>. The extractor always emits all six keys; a list is empty when the header
    /// is absent or unparseable. Group members are flattened in order.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<EmailParticipant>> Participants { get; init; }
        = new Dictionary<string, IReadOnlyList<EmailParticipant>>(StringComparer.Ordinal);
}
