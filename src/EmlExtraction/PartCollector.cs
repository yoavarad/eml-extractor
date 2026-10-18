using MimeKit;
using MimeKit.Tnef;

namespace EmlExtraction;

internal static class PartCollector
{
    public static IReadOnlyList<ExtractedAttachment> Collect(MimeEntity? root, TextPart? body, List<string> warnings)
    {
        var parts = new List<ExtractedAttachment>();
        if (root is not null)
            Walk(root, body, insideBodyAlternative: false, parts, warnings);
        return parts;
    }

    private static void Walk(
        MimeEntity entity,
        TextPart? body,
        bool insideBodyAlternative,
        List<ExtractedAttachment> parts,
        List<string> warnings)
    {
        switch (entity)
        {
            case TnefPart tnef:
                CollectTnef(tnef, body, parts, warnings);
                return;
            case MultipartAlternative alternative:
                var skipAlts = body is not null && Contains(alternative, body);
                foreach (var child in alternative)
                    Walk(child, body, insideBodyAlternative || skipAlts, parts, warnings);
                return;
            case Multipart multipart:
                foreach (var child in multipart)
                    Walk(child, body, insideBodyAlternative, parts, warnings);
                return;
            case MessagePart rfc822:
                AddRfc822(rfc822, parts, warnings);
                return;
            case TextPart text when ReferenceEquals(text, body):
                return;
            case TextPart text when insideBodyAlternative && !text.IsAttachment && (text.IsPlain || text.IsHtml):
                return;
            case MimePart part:
                AddMimePart(part, parts, warnings);
                return;
        }
    }

    private static void CollectTnef(TnefPart tnef, TextPart? body, List<ExtractedAttachment> parts, List<string> warnings)
    {
        try
        {
            foreach (var inner in tnef.ExtractAttachments())
                Walk(inner, body, insideBodyAlternative: false, parts, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"tnef_extract_failed: {ex.Message}");
            AddMimePart(tnef, parts, warnings);
        }
    }

    private static bool Contains(MimeEntity haystack, MimeEntity needle)
    {
        if (ReferenceEquals(haystack, needle))
            return true;
        return haystack is Multipart multipart && multipart.Any(child => Contains(child, needle));
    }

    private static void AddRfc822(MessagePart rfc822, List<ExtractedAttachment> parts, List<string> warnings)
    {
        try
        {
            using var stream = new MemoryStream();
            if (rfc822.Message is not null)
                rfc822.Message.WriteTo(stream);
            else
                rfc822.WriteTo(stream);

            Add(parts, rfc822, stream.ToArray(), fallback: "attached.eml");
        }
        catch (Exception ex)
        {
            warnings.Add($"rfc822_extract_failed: {ex.Message}");
        }
    }

    private static void AddMimePart(MimePart part, List<ExtractedAttachment> parts, List<string> warnings)
    {
        try
        {
            using var stream = new MemoryStream();
            part.Content?.DecodeTo(stream);
            Add(parts, part, stream.ToArray(), FallbackName(part, parts.Count));
        }
        catch (Exception ex)
        {
            warnings.Add($"part_decode_failed: {part.FileName ?? part.ContentType.MimeType}: {ex.Message}");
        }
    }

    private static void Add(List<ExtractedAttachment> parts, MimeEntity entity, byte[] data, string fallback)
    {
        var (name, fullPath) = ResolveName(entity, fallback);
        var location = entity.ContentLocation?.ToString().Trim();
        if (string.IsNullOrEmpty(location))
            location = null;
        parts.Add(new ExtractedAttachment(
            name,
            data.Length,
            fullPath,
            data,
            entity.ContentId,
            entity.ContentDisposition?.Disposition,
            fullPath,
            location));
    }

    private static string FallbackName(MimePart part, int index)
    {
        if (!string.IsNullOrWhiteSpace(part.ContentId))
        {
            var cid = part.ContentId.Trim('<', '>');
            var ext = string.IsNullOrEmpty(part.ContentType.MediaSubtype) ? "bin" : part.ContentType.MediaSubtype;
            return $"{cid}.{ext}";
        }

        var subtype = string.IsNullOrEmpty(part.ContentType.MediaSubtype) ? "bin" : part.ContentType.MediaSubtype;
        return $"part-{index}.{subtype}";
    }

    private static (string Name, string? FullPath) ResolveName(MimeEntity entity, string fallback)
    {
        var raw = (entity as MimePart)?.FileName
                  ?? entity.ContentDisposition?.FileName
                  ?? entity.ContentType.Parameters["name"];

        if (string.IsNullOrWhiteSpace(raw))
            return (fallback, null);

        raw = raw.Trim().Trim('"');
        if (!raw.Contains('/') && !raw.Contains('\\'))
            return (raw, null);

        var name = raw.Replace('\\', '/').Split('/')[^1];
        return (string.IsNullOrEmpty(name) ? fallback : name, raw);
    }
}
