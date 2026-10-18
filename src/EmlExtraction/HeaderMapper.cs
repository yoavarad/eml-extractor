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

        // MimeKit moves entity headers (Content-Type, ...) of a single-part message onto the body part.
        if (message.Body is not null)
            foreach (var header in message.Body.Headers.Where(h => h.Field.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)))
                Append(metadata, NormalizeKey(header.Field), header.Value ?? "");

        EnsureAddress(metadata, "from", Join(message.From));
        EnsureAddress(metadata, "to", Join(message.To));
        EnsureAddress(metadata, "cc", Join(message.Cc));
        EnsureAddress(metadata, "bcc", Join(message.Bcc));
        Set(metadata, "reply-to", Join(message.ReplyTo));
        Set(metadata, "sender", message.Sender?.ToString());
        Set(metadata, "subject", message.Subject);
        if (message.Date != DateTimeOffset.MinValue)
            Set(metadata, "date", message.Date.ToString("o"));
        Set(metadata, "message-id", message.MessageId);
        Set(metadata, "in-reply-to", message.InReplyTo);
        Set(metadata, "references", message.References.Count == 0 ? null : string.Join(" ", message.References));

        xfields = string.Join("\n", xLines);
        metadata["xfields"] = xfields;
        return metadata;
    }

    /// <summary>Fixed always-present keys with empty values (parse_failed, input_too_large, ...).</summary>
    internal static Dictionary<string, string> EmptyMetadata() => new(StringComparer.Ordinal)
    {
        ["from"] = "",
        ["to"] = "",
        ["cc"] = "",
        ["bcc"] = "",
        ["xfields"] = ""
    };

    private static string NormalizeKey(string field)
    {
        var key = field.Trim().ToLowerInvariant();
        return key == "xfields" ? "header:xfields" : key;
    }

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
}
