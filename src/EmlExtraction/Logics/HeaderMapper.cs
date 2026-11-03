using System.Collections.ObjectModel;
using EmlExtraction.Models;
using MimeKit;
using MimeKit.Utils;

namespace EmlExtraction.Logics;

internal static class HeaderMapper
{
    public static Dictionary<string, string> Map(MimeMessage message)
        => Map(message, out _);

    /// <summary>Header map plus structured participants built from the same effective address lists (ADR-025).</summary>
    public static Dictionary<string, string> Map(MimeMessage message, out Dictionary<string, IReadOnlyList<EmailParticipant>> participants)
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

        participants = EmptyParticipants();
        EnsureAddress(metadata, MetadataKeys.From, Effective(message, message.From, h => h.Id == HeaderId.From, participants, MetadataKeys.From));
        EnsureAddress(metadata, MetadataKeys.To, Effective(message, message.To, h => h.Id == HeaderId.To, participants, MetadataKeys.To));
        EnsureAddress(metadata, MetadataKeys.Cc, Effective(message, message.Cc, h => h.Id == HeaderId.Cc, participants, MetadataKeys.Cc));
        EnsureAddress(metadata, MetadataKeys.Bcc, Effective(message, message.Bcc, h => h.Id == HeaderId.Bcc, participants, MetadataKeys.Bcc));
        Func<Header, bool> isReplyTo = h => h.Id == HeaderId.ReplyTo || NormalizeKey(h.Field) == MetadataKeys.ReplyTo;
        var replyTo = message.Headers.Any(h => h.Id != HeaderId.ReplyTo && isReplyTo(h)) ? ParseHeaders(message, isReplyTo) : message.ReplyTo;
        Set(metadata, MetadataKeys.ReplyTo, Effective(message, replyTo, isReplyTo, participants, MetadataKeys.ReplyTo));
        Func<Header, bool> isRcptTo = h => RcptToNames.Contains(NormalizeKey(h.Field));
        Func<Header, bool> isMailFrom = h => MailFromNames.Contains(NormalizeKey(h.Field));
        Effective(message, ParseHeaders(message, isRcptTo), isRcptTo, participants, MetadataKeys.RcptTo);
        Effective(message, ParseHeaders(message, isMailFrom), isMailFrom, participants, MetadataKeys.MailFrom);
        if ((message.Sender is null || message.Sender.Address.Contains("=?", StringComparison.Ordinal))
            && message.Headers.Any(h => h.Id == HeaderId.Sender && h.RawValue.AsSpan().IndexOf("=?"u8) >= 0))
        {
            Set(metadata, MetadataKeys.Sender, Effective(message, message.Sender is null ? [] : [message.Sender], h => h.Id == HeaderId.Sender, participants, MetadataKeys.Sender));
        }
        else
        {
            Set(metadata, MetadataKeys.Sender, message.Sender?.ToString());
            if (message.Sender is not null)
                participants[MetadataKeys.Sender] = new ReadOnlyCollection<EmailParticipant>([ToParticipant(message.Sender)]);
        }
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

    /// <summary>All eight participant keys with empty lists (parse_failed, absent headers).</summary>
    internal static Dictionary<string, IReadOnlyList<EmailParticipant>> EmptyParticipants() => new(8, StringComparer.Ordinal)
    {
        [MetadataKeys.From] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.To] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.Cc] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.Bcc] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.ReplyTo] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.Sender] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.RcptTo] = ReadOnlyCollection<EmailParticipant>.Empty,
        [MetadataKeys.MailFrom] = ReadOnlyCollection<EmailParticipant>.Empty
    };

    /// <summary>Envelope header names (after <see cref="NormalizeKey"/>) that feed the rcptto / mailfrom participants.</summary>
    private static readonly string[] RcptToNames = ["rcptto", "rcpt-to", "rcpt_to"];
    private static readonly string[] MailFromNames = ["mailfrom", "mail-from", "mail_from"];

    /// <summary>Addresses of the matching headers in header order (for headers MimeKit does not parse itself).</summary>
    private static InternetAddressList ParseHeaders(MimeMessage message, Func<Header, bool> match)
    {
        var list = new InternetAddressList();
        foreach (var header in message.Headers.Where(match))
            if (InternetAddressList.TryParse(ParserOptions.Default, header.RawValue, out var parsed))
                list.AddRange(parsed);
        return list;
    }

    private static string NormalizeKey(string field)
    {
        var key = field.Trim().ToLowerInvariant();
        return key switch
        {
            "xfields" => "header:xfields",
            "reply_to" => MetadataKeys.ReplyTo,
            _ => key
        };
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
    /// Falls back to the decoded text when no mailbox results. The list used is also stored as participants.
    /// </summary>
    private static string? Effective(MimeMessage message, InternetAddressList list, Func<Header, bool> match,
        Dictionary<string, IReadOnlyList<EmailParticipant>> participants, string key)
    {
        var rawValues = message.Headers.Where(match).Select(h => h.RawValue).ToList();
        var encoded = rawValues.Any(r => r.AsSpan().IndexOf("=?"u8) >= 0);
        if (encoded && RecoverItems(rawValues) is { } recovered)
            return JoinAndStore(recovered, participants, key);
        if (!encoded || (list.Count > 0 && !list.Mailboxes.Any(m => m.Address?.Contains("=?", StringComparison.Ordinal) == true)))
            return JoinAndStore(list, participants, key);

        var decoded = string.Join(", ", rawValues.Select(r => Rfc2047.DecodeText(LooseOptions, r).Trim()).Where(t => t.Length > 0));
        if (InternetAddressList.TryParse(LooseOptions, decoded, out var reparsed) && reparsed.Mailboxes.Any())
            return JoinAndStore(reparsed, participants, key);
        return string.IsNullOrWhiteSpace(decoded) ? JoinAndStore(list, participants, key) : decoded;
    }

    private static string? JoinAndStore(InternetAddressList list, Dictionary<string, IReadOnlyList<EmailParticipant>> participants, string key)
    {
        if (list.Count > 0)
        {
            var found = ToReadOnly(list);
            if (found.Count > 0)
                participants[key] = found;
        }

        return Join(list);
    }

    /// <summary>
    /// Only for headers holding an encoded-word. Splits the raw value into top-level address items (commas inside quotes,
    /// comments, angle brackets and groups do not split), decodes and parses each, and keeps an item that yields nothing
    /// as a raw mailbox (Address = raw item) in its original position. Returns null when every item parsed.
    /// </summary>
    private static InternetAddressList? RecoverItems(List<byte[]> rawValues)
    {
        var merged = new InternetAddressList();
        var anyRaw = false;
        foreach (var bytes in rawValues)
        {
            foreach (var item in SplitItems(System.Text.Encoding.Latin1.GetString(bytes)))
                anyRaw |= RecoverItem(item, merged);
        }

        return anyRaw ? merged : null;
    }

    /// <summary>
    /// Parses one top-level item into target, or keeps it raw. A group is first split member by member (MimeKit silently drops a bad member)
    /// so a bad member stays a raw mailbox in place, and used only when one was kept; its display name never becomes a participant. True when a raw token was kept.
    /// </summary>
    private static bool RecoverItem(string item, InternetAddressList target)
    {
        if (IsCommentOnly(item))
            return false;

        if (TrySplitGroup(item, out var name, out var members))
        {
            var group = ParseGroupHead(name);
            var anyRaw = false;
            foreach (var member in SplitItems(members))
                anyRaw |= RecoverItem(member, group.Members);
            if (anyRaw)
            {
                target.Add(group);
                return true;
            }
        }

        var itemBytes = System.Text.Encoding.Latin1.GetBytes(item);
        if (InternetAddressList.TryParse(LooseOptions, itemBytes, out var direct) && direct.Count > 0
            && !direct.Mailboxes.Any(m => m.Address?.Contains("=?", StringComparison.Ordinal) == true))
        {
            target.AddRange(direct);
            return false;
        }

        var decoded = Rfc2047.DecodeText(LooseOptions, itemBytes).Trim();
        if (decoded.Length > 0 && !decoded.Contains("=?", StringComparison.Ordinal)
            && InternetAddressList.TryParse(LooseOptions, decoded, out var parsed) && parsed.Count > 0)
        {
            target.AddRange(parsed);
            return false;
        }

        if (item.Contains("=?", StringComparison.Ordinal))
        {
            target.Add(new RawMailbox(item.Replace("\r", "").Replace("\n", " ").Trim()));
            return true;
        }

        return false;
    }

    /// <summary>True when the item is only whitespace and comments, e.g. the CFWS after a group's closing semicolon.</summary>
    private static bool IsCommentOnly(string item)
    {
        var comment = 0;
        for (var i = 0; i < item.Length; i++)
        {
            var c = item[i];
            if (comment > 0)
            {
                if (c == '\\')
                    i++;
                else
                    comment += c == '(' ? 1 : c == ')' ? -1 : 0;
            }
            else if (c == '(')
                comment = 1;
            else if (!char.IsWhiteSpace(c))
                return false;
        }

        return true;
    }

    private static GroupAddress ParseGroupHead(string name)
    {
        var head = System.Text.Encoding.Latin1.GetBytes(name + ":;");
        if (InternetAddressList.TryParse(LooseOptions, head, out var list) && list.Count == 1 && list[0] is GroupAddress g)
            return g;
        return new GroupAddress(Rfc2047.DecodeText(LooseOptions, System.Text.Encoding.Latin1.GetBytes(name)).Trim());
    }

    /// <summary>Splits "name: members;" at the first top-level colon (not in quotes, comments or angle brackets).</summary>
    private static bool TrySplitGroup(string item, out string name, out string members)
    {
        name = members = "";
        var trimmed = item.TrimEnd();
        if (!trimmed.EndsWith(';'))
            return false;
        var quoted = false;
        var comment = 0;
        var angle = false;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '\\' && (quoted || comment > 0))
                i++;
            else if (quoted)
                quoted = c != '"';
            else if (comment > 0)
                comment += c == '(' ? 1 : c == ')' ? -1 : 0;
            else if (c == '"')
                quoted = true;
            else if (c == '(')
                comment = 1;
            else if (c == '<')
                angle = true;
            else if (c == '>')
                angle = false;
            else if (c == ':' && !angle)
            {
                name = trimmed[..i];
                members = trimmed[(i + 1)..^1];
                return true;
            }
        }

        return false;
    }

    /// <summary>Marks an undecodable raw item kept as a mailbox: participant has Address only.</summary>
    private sealed class RawMailbox(string raw) : MailboxAddress("", raw);

    private static IEnumerable<string> SplitItems(string raw)
    {
        var start = 0;
        var quoted = false;
        var comment = 0;
        var angle = false;
        var group = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && (quoted || comment > 0))
                i++;
            else if (quoted)
                quoted = c != '"';
            else if (comment > 0)
                comment += c == '(' ? 1 : c == ')' ? -1 : 0;
            else if (c == '"')
                quoted = true;
            else if (c == '(')
                comment = 1;
            else if (c == '<')
                angle = true;
            else if (c == '>')
                angle = false;
            else if (angle)
            {
            }
            else if (c == ':')
                group = true;
            else if (c == ';' && group)
            {
                group = false;
                if (raw[start..(i + 1)].Trim() is { Length: > 0 } g)
                    yield return g;
                start = i + 1;
            }
            else if (c == ',' && !group)
            {
                if (raw[start..i].Trim() is { Length: > 0 } item)
                    yield return item;
                start = i + 1;
            }
        }

        if (raw[start..].Trim().Trim(',') is { Length: > 0 } last)
            yield return last;
    }

    /// <summary>Common case (no groups): fill one array and wrap it, skipping the intermediate List.</summary>
    private static ReadOnlyCollection<EmailParticipant> ToReadOnly(InternetAddressList list)
    {
        var flat = true;
        for (var i = 0; i < list.Count && flat; i++)
            flat = list[i] is MailboxAddress;
        if (flat)
        {
            var array = new EmailParticipant[list.Count];
            for (var i = 0; i < array.Length; i++)
                array[i] = ToParticipant((MailboxAddress)list[i]);
            return new ReadOnlyCollection<EmailParticipant>(array);
        }

        var found = new List<EmailParticipant>(list.Count);
        AddMailboxes(list, found);
        return found.AsReadOnly();
    }

    /// <summary>Mailboxes in order, group members flattened (group name dropped). Index loop: no LINQ/iterator allocation.</summary>
    private static void AddMailboxes(InternetAddressList list, List<EmailParticipant> found)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is MailboxAddress mailbox)
                found.Add(ToParticipant(mailbox));
            else if (list[i] is GroupAddress group)
                AddMailboxes(group.Members, found);
        }
    }

    /// <summary>Split at the last <c>@</c>; no <c>@</c> gives User = Address and Domain = "". No case or IDN change.</summary>
    private static EmailParticipant ToParticipant(MailboxAddress mailbox)
    {
        var address = mailbox.Address ?? "";
        var at = address.LastIndexOf('@');
        if (mailbox is RawMailbox)
            return new EmailParticipant("", address, "", "");
        return at < 0
            ? new EmailParticipant(mailbox.Name ?? "", address, address, "")
            : new EmailParticipant(mailbox.Name ?? "", address, address[..at], address[(at + 1)..]);
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
