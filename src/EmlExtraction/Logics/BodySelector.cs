using MimeKit;

namespace EmlExtraction.Logics;

internal static class BodySelector
{
    public static TextPart? Select(MimeEntity? entity)
    {
        switch (entity)
        {
            case TextPart part when part.IsAttachment:
                return null;
            case TextPart part when part.IsHtml || part.IsPlain:
                return part;
            case TextPart part when part.ContentType.IsMimeType("text", "calendar"):
                return part;
            case MultipartAlternative alternative:
                return PickAlternative(alternative);
            case MultipartRelated related:
                return Select(related.Root) ?? related.Select(Select).FirstOrDefault(p => p is not null);
            case Multipart multipart:
                return multipart.Select(Select).FirstOrDefault(p => p is not null);
            default:
                return null;
        }
    }

    private static TextPart? PickAlternative(MultipartAlternative alternative)
    {
        TextPart? html = null;
        TextPart? plain = null;
        TextPart? other = null;
        foreach (var child in alternative)
        {
            var selected = Select(child);
            if (selected is null)
                continue;
            if (selected.IsHtml)
                html = selected;
            else if (selected.IsPlain)
                plain = selected;
            else
                other ??= selected;
        }

        return html ?? plain ?? other;
    }
}
