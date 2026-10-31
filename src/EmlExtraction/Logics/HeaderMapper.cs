using EmlExtraction.Models;
using MimeKit;
using MimeKit.Utils;

namespace EmlExtraction.Logics;

internal static class HeaderMapper
{
    public static Dictionary<string, string> Map(MimeMessage message)
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

        EnsureAddress(metadata, MetadataKeys.From, Effective(message, message.From, HeaderId.From));
        EnsureAddress(metadata, MetadataKeys.To, Effective(message, message.To, HeaderId.To));
        EnsureAddress(metadata, MetadataKeys.Cc, Effective(message, message.Cc, HeaderId.Cc));
        EnsureAddress(metadata, MetadataKeys.Bcc, Effective(message, message.Bcc, HeaderId.Bcc));
        Set(metadata, MetadataKeys.ReplyTo, Effective(message, message.ReplyTo, HeaderId.ReplyTo));
        Set(metadata, MetadataKeys.Sender, (message.Sender is null || message.Sender.Address.Contains("=?", StringComparison.Ordinal))
            && message.Headers.Any(h => h.Id == HeaderId.Sender && h.RawValue.AsSpan().IndexOf("=?"u8) >= 0)
            ? Effective(message, message.Sender is null ? [] : [message.Sender], HeaderId.Sender)
            : message.Sender?.ToString());
        Set(metadata, MetadataKeys.Subject, message.Subject);
        if (message.Date != DateTimeOffset.MinValue)
            Set(metadata, MetadataKeys.Date, message.Date.ToString("o"));
        Set(metadata, MetadataKeys.MessageId, message.MessageId);
        Set(metadata, MetadataKeys.InReplyTo, message.InReplyTo);
        Set(metadata, MetadataKeys.References, message.References.Count == 0 ? null : string.Join(" ", message.References));

        metadata[MetadataKeys.XFields] = string.Join("\n", xLines);
        return metadata;
    }

    /// <summary>Fixed always-present keys with empty values (parse_failed, ...).</summary>
    internal static Dictionary<string, string> EmptyMetadata() => new(StringComparer.Ordinal)
    {
        [MetadataKeys.From] = "",
        [MetadataKeys.To] = "",
        [MetadataKeys.Cc] = "",
        [MetadataKeys.Bcc] = "",
        [MetadataKeys.XFields] = ""
    };

    private static string NormalizeKey(string field)
    {
        var key = field.Trim().ToLowerInvariant();
        return key == "xfields" ? "header:xfields" : key;
    }

    private static bool IsXHeader(string field)
        => field.StartsWith("X_", StringComparison.Ordinal);

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

    private static readonly ParserOptions LooseOptions = CreateLooseOptions();

    private static ParserOptions CreateLooseOptions()
    {
        var options = ParserOptions.Default.Clone();
        options.Rfc2047ComplianceMode = RfcComplianceMode.Loose;
        return options;
    }

    /// <summary>
    /// Effective address text for a participant header. If the raw value holds an encoded-word that MimeKit
    /// could not resolve (empty list or an address still containing "=?"), decode the raw value and re-parse it.
    /// Falls back to the decoded text when no mailbox results.
    /// </summary>
    private static string? Effective(MimeMessage message, InternetAddressList list, HeaderId id)
    {
        var rawValues = message.Headers.Where(h => h.Id == id).Select(h => h.RawValue).ToList();
        var encoded = rawValues.Any(r => r.AsSpan().IndexOf("=?"u8) >= 0);
        if (!encoded || (list.Count > 0 && !list.Mailboxes.Any(m => m.Address?.Contains("=?", StringComparison.Ordinal) == true)))
            return Join(list);

        var decoded = string.Join(", ", rawValues.Select(r => Rfc2047.DecodeText(LooseOptions, r).Trim()).Where(t => t.Length > 0));
        if (InternetAddressList.TryParse(LooseOptions, decoded, out var reparsed) && reparsed.Mailboxes.Any())
            return Join(reparsed);
        return string.IsNullOrWhiteSpace(decoded) ? Join(list) : decoded;
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
