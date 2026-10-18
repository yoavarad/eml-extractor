namespace EmlExtraction;

/// <summary>One collected MIME part: attachment, inline image, nested message, TNEF-unpacked file or non-selected body part.</summary>
/// <param name="Name">File name to present for this part (basename or a generated fallback).</param>
/// <param name="Size">Decoded size in bytes; equals <c>Data.Length</c>.</param>
/// <param name="FullPath">Path-bearing file name as written in the message, or <see langword="null"/>.</param>
/// <param name="Data">Decoded part bytes.</param>
/// <param name="ContentId">Content-ID without angle brackets, or <see langword="null"/>.</param>
/// <param name="Disposition">Content-Disposition value (<c>attachment</c>, <c>inline</c>), or <see langword="null"/>.</param>
public sealed record ExtractedAttachment(
    string Name,
    long Size,
    string? FullPath,
    byte[] Data,
    string? ContentId,
    string? Disposition);
