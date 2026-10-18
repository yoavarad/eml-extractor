namespace EmlExtraction;

/// <summary>One collected MIME part: attachment, inline image, nested message, TNEF-unpacked file or non-selected body part.</summary>
/// <param name="FileName">File name to present for this part (basename or a generated fallback).</param>
/// <param name="SizeInBytes">Decoded size in bytes; equals <c>BinaryData.Length</c>.</param>
/// <param name="FullPath">Path-bearing file name as written in the message, or <see langword="null"/>.</param>
/// <param name="BinaryData">Decoded part bytes.</param>
/// <param name="ContentId">Content-ID without angle brackets, or <see langword="null"/>.</param>
/// <param name="Disposition">Content-Disposition value (<c>attachment</c>, <c>inline</c>), or <see langword="null"/>.</param>
/// <param name="OriginalPath">Raw trimmed sender file name when it contains <c>/</c> or <c>\</c>, else <see langword="null"/>.</param>
/// <param name="ContentLocation">Decoded, trimmed Content-Location header, or <see langword="null"/>.</param>
public sealed record ExtractedAttachment(
    string FileName,
    long SizeInBytes,
    string? FullPath,
    byte[] BinaryData,
    string? ContentId,
    string? Disposition,
    string? OriginalPath,
    string? ContentLocation);
