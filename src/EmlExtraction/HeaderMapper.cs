using MimeKit;

namespace EmlExtraction;

internal static class HeaderMapper
{
    public static IReadOnlyDictionary<string, string> Map(MimeMessage message, out string xfields)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var xLines = new List<string>();

        foreach (var header in message.Headers)
        {
            var value = header.Value ?? "";
            Append(metadata, NormalizeKey(header.Field), value);
            if (IsXHeader(header.Field))
                xLines.Add($"{header.Field}: {value}");
        }

        Set(metadata, "from", Join(message.From));
        Set(metadata, "to", Join(message.To));
        Set(metadata, "cc", Join(message.Cc));
        Set(metadata, "bcc", Join(message.Bcc));
        Set(metadata, "replyto", Join(message.ReplyTo));
        Set(metadata, "sender", message.Sender?.ToString());
        Set(metadata, "subject", message.Subject);
        if (message.Date != DateTimeOffset.MinValue)
            Set(metadata, "date", message.Date.ToString("o"));
        Set(metadata, "messageid", message.MessageId);
        Set(metadata, "inreplyto", message.InReplyTo);
        Set(metadata, "references", message.References.Count == 0 ? null : string.Join(" ", message.References));
        Set(metadata, "mimeversion", message.Headers[HeaderId.MimeVersion]);
        Set(metadata, "contenttype", message.Body?.ContentType?.MimeType);
        Set(metadata, "returnpath", First(message, "Return-Path"));
        Set(metadata, "deliveredto", First(message, "Delivered-To"));

        var received = string.Join("\n", message.Headers.Where(h => h.Id == HeaderId.Received).Select(h => h.Value));
        Set(metadata, "received", string.IsNullOrEmpty(received) ? null : received);

        xfields = string.Join("\n", xLines);
        return metadata;
    }

    private static string NormalizeKey(string field)
        => field.Trim().ToLowerInvariant().Replace('-', '_');

    private static bool IsXHeader(string field)
        => field.Length >= 2 && field[0] is 'X' or 'x' && field[1] is '-' or '_';

    private static void Append(Dictionary<string, string> map, string key, string value)
        => map[key] = map.TryGetValue(key, out var existing) ? existing + "\n" + value : value;

    private static void Set(Dictionary<string, string> map, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            map[key] = value;
    }

    private static string? Join(InternetAddressList list)
        => list.Count == 0 ? null : string.Join("; ", list);

    private static string? First(MimeMessage message, string name)
        => message.Headers.FirstOrDefault(h => h.Field.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
}
