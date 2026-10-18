using MimeKit;
using MimeKit.Tnef;

namespace EmlExtraction;

/// <summary>Collects attachment records in ADR-013 processing order and assigns unique container paths.</summary>
internal static class PartCollector
{
    private const string TnefSegment = "winmail.dat";

    public static IReadOnlyList<ExtractedAttachment> Collect(MimeEntity? root, TextPart? body, List<string> warnings)
    {
        var parts = new List<ExtractedAttachment>();
        if (root is not null)
            Walk(root, body, insideBodyAlternative: false, container: null, parts, warnings);
        AssignUniquePaths(parts);
        return parts;
    }

    private static void Walk(
        MimeEntity entity,
        TextPart? body,
        bool insideBodyAlternative,
        string? container,
        List<ExtractedAttachment> parts,
        List<string> warnings)
    {
        switch (entity)
        {
            case TnefPart tnef:
                CollectTnef(tnef, body, container, parts, warnings);
                return;
            case MultipartAlternative alternative:
                var skipAlts = body is not null && Contains(alternative, body);
                foreach (var child in alternative)
                    Walk(child, body, insideBodyAlternative || skipAlts, container, parts, warnings);
                return;
            case Multipart multipart:
                foreach (var child in multipart)
                    Walk(child, body, insideBodyAlternative, container, parts, warnings);
                return;
            case MessagePart rfc822:
                AddRfc822(rfc822, container, parts, warnings);
                return;
            case TextPart text when ReferenceEquals(text, body):
                return;
            case TextPart text when insideBodyAlternative && !text.IsAttachment && (text.IsPlain || text.IsHtml):
                return;
            case MimePart part:
                AddMimePart(part, container, parts, warnings);
                return;
        }
    }

    private static void CollectTnef(TnefPart tnef, TextPart? body, string? container, List<ExtractedAttachment> parts, List<string> warnings)
    {
        try
        {
            var segment = Join(container, ResolveName(tnef, TnefSegment).Name);
            foreach (var entity in tnef.ExtractAttachments())
                Walk(entity, body, insideBodyAlternative: false, segment, parts, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"tnef_extract_failed: {ex.Message}");
            AddMimePart(tnef, container, parts, warnings);
        }
    }

    private static bool Contains(MimeEntity haystack, MimeEntity needle)
    {
        if (ReferenceEquals(haystack, needle))
            return true;
        return haystack is Multipart multipart && multipart.Any(child => Contains(child, needle));
    }

    private static void AddRfc822(MessagePart rfc822, string? container, List<ExtractedAttachment> parts, List<string> warnings)
    {
        try
        {
            using var stream = new MemoryStream();
            if (rfc822.Message is not null)
                rfc822.Message.WriteTo(stream);
            else
                rfc822.WriteTo(stream);

            Add(parts, rfc822, stream.ToArray(), fallback: "attached.eml", container);
        }
        catch (Exception ex)
        {
            warnings.Add($"rfc822_extract_failed: {ex.Message}");
        }
    }

    private static void AddMimePart(MimePart part, string? container, List<ExtractedAttachment> parts, List<string> warnings)
    {
        try
        {
            using var stream = new MemoryStream();
            part.Content?.DecodeTo(stream);
            Add(parts, part, stream.ToArray(), FallbackName(part, parts.Count), container);
        }
        catch (Exception ex)
        {
            warnings.Add($"part_decode_failed: {part.FileName ?? part.ContentType.MimeType}: {ex.Message}");
        }
    }

    /// <summary>Pass 1: records carry their literal path (container segments + FileName).</summary>
    private static void Add(List<ExtractedAttachment> parts, MimeEntity entity, byte[] data, string fallback, string? container)
    {
        var (name, originalPath) = ResolveName(entity, fallback);
        var location = entity.ContentLocation?.ToString().Trim();
        if (string.IsNullOrEmpty(location))
            location = null;
        parts.Add(new ExtractedAttachment(
            name,
            data.Length,
            Join(container, name),
            data,
            entity.ContentId,
            entity.ContentDisposition?.Disposition,
            originalPath,
            location));
    }

    private static string Join(string? container, string segment) => container is null ? segment : $"{container}/{segment}";

    /// <summary>Pass 2: first record keeps its literal path; later duplicates get the smallest free <c>~n</c> (n &gt;= 2) not reserved by any literal path.</summary>
    private static void AssignUniquePaths(List<ExtractedAttachment> parts)
    {
        var reserved = new HashSet<string>(parts.Select(p => p.FullPath), StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < parts.Count; i++)
        {
            var path = parts[i].FullPath;
            if (assigned.Add(path))
                continue;
            for (var n = 2; ; n++)
            {
                var candidate = WithSuffix(path, n);
                if (!reserved.Contains(candidate) && assigned.Add(candidate))
                {
                    parts[i] = parts[i] with { FullPath = candidate };
                    break;
                }
            }
        }
    }

    private static string WithSuffix(string path, int n)
    {
        var segmentStart = path.LastIndexOf('/') + 1;
        var dot = path.LastIndexOf('.');
        return dot > segmentStart ? $"{path[..dot]}~{n}{path[dot..]}" : $"{path}~{n}";
    }

    private static string FallbackName(MimePart part, int index)
    {
        var subtype = string.IsNullOrEmpty(part.ContentType.MediaSubtype) ? "bin" : part.ContentType.MediaSubtype;
        if (!string.IsNullOrWhiteSpace(part.ContentId))
            return Sanitise($"{part.ContentId.Trim('<', '>')}.{subtype}");
        return Sanitise($"part-{index}.{subtype}");
    }

    /// <summary>Replaces / \ : * ? " &lt; &gt; | and control characters with '_' so a generated name never splits the path.</summary>
    private static string Sanitise(string name)
        => string.Create(name.Length, name, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or <= '\u001F' or '\u007F'
                    ? '_'
                    : c;
            }
        });

    private static (string Name, string? OriginalPath) ResolveName(MimeEntity entity, string fallback)
    {
        var raw = (entity as MimePart)?.FileName
                  ?? entity.ContentDisposition?.FileName
                  ?? entity.ContentType.Parameters["name"];

        if (string.IsNullOrWhiteSpace(raw))
            return (fallback, null);

        raw = raw.Trim().Trim('"');
        var hasPath = raw.Contains('/') || raw.Contains('\\');
        var name = hasPath ? raw.Replace('\\', '/').Split('/')[^1].Trim().Trim('"') : raw;
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            name = fallback;
        return (name, hasPath ? raw : null);
    }
}
