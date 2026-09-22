using MimeKit;

namespace EmlExtraction;

internal static class HeaderMapper
{
    public static Dictionary<string, string> Map(MimeMessage message, out string xfields)
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

        EnsureAddress(metadata, "from", Join(message.From));
        EnsureAddress(metadata, "to", Join(message.To));
        EnsureAddress(metadata, "cc", Join(message.Cc));
        EnsureAddress(metadata, "bcc", Join(message.Bcc));
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

    /// <summary>
    /// Parsed mailbox list wins. If MimeKit found none, keep the raw header.
    /// Always emit the key so callers can read from/to/cc/bcc without a lookup miss.
    /// </summary>
    private static void EnsureAddress(Dictionary<string, string> map, string key, string? parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed))
        {
            map[key] = parsed;
            return;
        }

        if (!map.ContainsKey(key))
            map[key] = "";
    }

    private static string? Join(InternetAddressList list)
        => list.Count == 0 ? null : string.Join("; ", list.Select(FormatAddress));

    private static string FormatAddress(InternetAddress address) => address switch
    {
        MailboxAddress mailbox => string.IsNullOrEmpty(mailbox.Name)
            ? (mailbox.Address ?? "")
            : $"{mailbox.Name} <{mailbox.Address}>",
        GroupAddress group => $"{group.Name}: {string.Join("; ", group.Members.Select(FormatAddress))};",
        _ => address.ToString()
    };

    private static string? First(MimeMessage message, string name)
        => message.Headers.FirstOrDefault(h => h.Field.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
}
