using System.Security.Cryptography;

namespace EmlExtraction.Tests;

/// <summary>Flattens a result into comparable lines (records hold byte[] and collections, so == is reference-based).</summary>
internal static class ResultProjection
{
    public static IReadOnlyList<string> Of(EmlExtractionResult result)
    {
        var lines = new List<string>();
        lines.AddRange(result.Metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"meta {kv.Key}={kv.Value}"));
        lines.Add($"content {result.MainContent}");
        lines.Add($"mime {result.MainContentMimeType}");
        lines.Add($"dir {result.SubjectDirection}/{result.ContentDirection}");
        lines.AddRange(result.Attachments.Select(a =>
            $"att {a.FileName}|{a.SizeInBytes}|{a.FullPath}|{Convert.ToHexString(SHA256.HashData(a.BinaryData))}|{a.ContentId}|{a.Disposition}|{a.OriginalPath}|{a.ContentLocation}"));
        lines.AddRange(result.Warnings.Select(w => $"warn {w}"));
        return lines;
    }
}
