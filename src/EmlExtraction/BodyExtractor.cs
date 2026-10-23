using MimeKit;

namespace EmlExtraction;

/// <summary>Body and attachments stage bodies (ADR-004), without their wrappers; the profiling harness (ADR-020) calls them directly.</summary>
internal sealed class BodyExtractor
{
    /// <summary>Body stage result.</summary>
    /// <param name="Part">The selected main-content part, or null; the attachments stage excludes it.</param>
    /// <param name="Content">Decoded main content with trailing line breaks trimmed, or null.</param>
    /// <param name="ContentType">MIME type of the main content, or null.</param>
    /// <param name="SubjectDirection">Text direction of the subject.</param>
    /// <param name="ContentDirection">Text direction of the main content.</param>
    internal readonly record struct BodyContent(
        TextPart? Part,
        string? Content,
        string? ContentType,
        string SubjectDirection,
        string ContentDirection);

    /// <summary>Selects and decodes the main content and detects subject and content direction.</summary>
    /// <param name="message">Parsed message; if it was parsed persistently, its stream must stay open.</param>
    /// <param name="subject">The mapped subject header, for its direction.</param>
    /// <param name="warnings">Receives <c>body_decode_failed</c> when the content cannot be decoded.</param>
    internal BodyContent ExtractBody(MimeMessage message, string? subject, List<string> warnings)
    {
        var body = BodySelector.Select(message.Body);
        string? content = null;
        string? contentType = null;
        if (body is not null)
        {
            try
            {
                content = body.Text.TrimEnd('\r', '\n');
                contentType = body.ContentType.MimeType;
            }
            catch (Exception ex) when (EmlExtractor.IsContentFailure(ex))
            {
                warnings.Add($"body_decode_failed: {ex.Message}");
            }
        }

        var html = contentType is not null
                   && contentType.Contains("html", StringComparison.OrdinalIgnoreCase);
        return new BodyContent(
            body,
            content,
            contentType,
            TextDirection.Detect(subject, html: false),
            TextDirection.Detect(content, html));
    }

    /// <summary>Collects the attachments of <paramref name="message"/>, excluding the main-content <paramref name="body"/>.</summary>
    internal IReadOnlyList<ExtractedAttachment> ExtractAttachments(MimeMessage message, TextPart? body, List<string> warnings)
        => PartCollector.Collect(message.Body, body, warnings);
}
