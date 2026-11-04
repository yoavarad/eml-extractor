using System.Collections.ObjectModel;
using EmlExtraction.Models;
using MimeKit;
using MimeKit.Utils;

namespace EmlExtraction.Logics;

internal static class HeaderMapper
{
    public static Dictionary<string, string> Map(MimeMessage message)
        => Map(message, ParserOptions.Default, [], out _);

    /// <summary>
    /// Header map plus structured participants built from the same effective address lists (ADR-025).
    /// <paramref name="options"/> are the options the message was parsed with; headers MimeKit does not parse itself use them too.
    /// Each raw header and each key is guarded on its own (ADR-026): a failure loses only that key and adds its warning to
    /// <paramref name="warnings"/>; the fixed keys keep "" and a failed participant list stays empty.
    /// </summary>
    public static Dictionary<string, string> Map(MimeMessage message, ParserOptions options, List<string> warnings,
        out Dictionary<string, IReadOnlyList<EmailParticipant>> participants)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var xLines = new List<string>();

        foreach (var header in message.Headers)
        {
            var key = NormalizeKey(header.Field);
            try
            {
                HeaderFault(key);
                var value = header.Value ?? "";
                Append(metadata, key, value);
                if (IsXHeader(header.Field))
                    xLines.Add($"{header.Field}: {value}");
            }
            catch (Exception ex) when (EmlExtractor.IsBug(ex))
            {
                FieldFailed(key, ex, warnings);
            }
        }

        // MimeKit moves entity headers (Content-Type, ...) of a single-part message onto the body part.
        if (message.Body is not null)
        {
            foreach (var header in message.Body.Headers.Where(h => h.Field.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)))
            {
                var key = NormalizeKey(header.Field);
                try
                {
                    HeaderFault(key);
                    Append(metadata, key, header.Value ?? "");
                }
                catch (Exception ex) when (EmlExtractor.IsBug(ex))
                {
                    FieldFailed(key, ex, warnings);
                }
            }
        }

        participants = EmptyParticipants();
        MapAddress(message, message.From, static h => h.Id == HeaderId.From, metadata, participants, MetadataKeys.From, warnings);
        MapAddress(message, message.To, static h => h.Id == HeaderId.To, metadata, participants, MetadataKeys.To, warnings);
        MapAddress(message, message.Cc, static h => h.Id == HeaderId.Cc, metadata, participants, MetadataKeys.Cc, warnings);
        MapAddress(message, message.Bcc, static h => h.Id == HeaderId.Bcc, metadata, participants, MetadataKeys.Bcc, warnings);

        try
        {
            FieldFault(MetadataKeys.ReplyTo);
            Func<Header, bool> isReplyTo = h => h.Id == HeaderId.ReplyTo || NormalizeKey(h.Field) == MetadataKeys.ReplyTo;
            var replyTo = message.Headers.Any(h => h.Id != HeaderId.ReplyTo && isReplyTo(h)) ? ParseHeaders(message, options, isReplyTo) : message.ReplyTo;
            Set(metadata, MetadataKeys.ReplyTo, Effective(message, replyTo, isReplyTo, participants, MetadataKeys.ReplyTo));
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.ReplyTo, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.RcptTo);
            Func<Header, bool> isRcptTo = h => RcptToNames.Contains(NormalizeKey(h.Field));
            if (message.Headers.Any(isRcptTo))
                Effective(message, ParseHeaders(message, options, isRcptTo), isRcptTo, participants, MetadataKeys.RcptTo);
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.RcptTo, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.MailFrom);
            Func<Header, bool> isMailFrom = h => MailFromNames.Contains(NormalizeKey(h.Field));
            if (message.Headers.Any(isMailFrom))
                Effective(message, ParseHeaders(message, options, isMailFrom), isMailFrom, participants, MetadataKeys.MailFrom);
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.MailFrom, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.Sender);
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
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.Sender, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.Subject);
            Set(metadata, MetadataKeys.Subject, message.Subject);
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.Subject, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.Date);
            if (message.Date != DateTimeOffset.MinValue)
                Set(metadata, MetadataKeys.Date, message.Date.ToString("o"));
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.Date, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.MessageId);
            Set(metadata, MetadataKeys.MessageId, message.MessageId);
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.MessageId, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.InReplyTo);
            Set(metadata, MetadataKeys.InReplyTo, message.InReplyTo);
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.InReplyTo, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.References);
            Set(metadata, MetadataKeys.References, message.References.Count == 0 ? null : string.Join(" ", message.References));
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.References, ex, warnings, metadata, participants);
        }

        try
        {
            FieldFault(MetadataKeys.XFields);
            metadata[MetadataKeys.XFields] = string.Join("\n", xLines);
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(MetadataKeys.XFields, ex, warnings, metadata, participants);
        }

        return metadata;
    }

    /// <summary>from / to / cc / bcc: the string and the participant list are guarded together.</summary>
    private static void MapAddress(MimeMessage message, InternetAddressList list, Func<Header, bool> match, Dictionary<string, string> metadata,
        Dictionary<string, IReadOnlyList<EmailParticipant>> participants, string key, List<string> warnings)
    {
        try
        {
            FieldFault(key);
            EnsureAddress(metadata, key, Effective(message, list, match, participants, key));
        }
        catch (Exception ex) when (EmlExtractor.IsBug(ex))
        {
            FieldFailed(key, ex, warnings, metadata, participants);
        }
    }

    /// <summary>Test seam point <c>metadata.&lt;key&gt;</c> for one key; builds the name only when a fault is set.</summary>
    private static void FieldFault(string key)
    {
        if (EmlExtractor.StageFault is { } fault)
            fault("metadata." + key);
    }

    /// <summary>Test seam point <c>metadata.header.&lt;key&gt;</c> for one raw header append.</summary>
    private static void HeaderFault(string key)
    {
        if (EmlExtractor.StageFault is { } fault)
            fault("metadata.header." + key);
    }

    /// <summary>
    /// Records a failed key (ADR-026): a content exception gives <c>metadata_field_failed: &lt;key&gt;: &lt;Type&gt;</c>, any
    /// other one <c>internal_error: metadata.&lt;key&gt;: &lt;Type&gt;</c> (counted under stage metadata). Never the exception message.
    /// </summary>
    private static void FieldFailed(string key, Exception ex, List<string> warnings)
    {
        if (EmlExtractor.IsContentFailure(ex))
            warnings.Add($"metadata_field_failed: {key}: {ex.GetType().Name}");
        else
            EmlExtractor.RecordBug("metadata." + key, "metadata", ex, warnings);
    }

    /// <summary>Records a failed key and resets it: a fixed key keeps "", any other key is omitted, its participant list is empty.</summary>
    private static void FieldFailed(string key, Exception ex, List<string> warnings, Dictionary<string, string> metadata,
        Dictionary<string, IReadOnlyList<EmailParticipant>> participants)
    {
        FieldFailed(key, ex, warnings);
        if (key is MetadataKeys.From or MetadataKeys.To or MetadataKeys.Cc or MetadataKeys.Bcc or MetadataKeys.XFields)
            metadata[key] = "";
        else
            metadata.Remove(key);
        if (participants.ContainsKey(key))
            participants[key] = ReadOnlyCollection<EmailParticipant>.Empty;
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

    /// <summary>
    /// Addresses of the matching headers in header order, for headers MimeKit does not parse itself. Each raw value is
    /// re-read as a To header under <paramref name="options"/>, so it gets exactly MimeKit's To result (partial lists kept).
    /// </summary>
    private static InternetAddressList ParseHeaders(MimeMessage message, ParserOptions options, Func<Header, bool> match)
    {
        using var block = new MemoryStream();
        foreach (var header in message.Headers.Where(match))
        {
            block.Write("To:"u8);
            block.Write(header.RawValue);
            if (header.RawValue is not [.., (byte)'\n'])
                block.Write("\r\n"u8);
        }

        block.Write("\r\n"u8);
        block.Position = 0;
        try
        {
            return MimeMessage.Load(options, block).To;
        }
        catch (Exception ex) when (EmlExtractor.IsContentFailure(ex))
        {
            return [];
        }
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
    /// comments, angle brackets, groups and encoded-words do not split), decodes and parses each, and keeps an item that yields nothing
    /// as a raw mailbox (Address = the item's addr-spec, or the item text when it is none) in its original position. Returns null when MimeKit parsed every item itself.
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
    /// so a bad member stays a raw mailbox in place, and used only when one was kept; its display name never becomes a participant. True when a raw token was kept or the item needed recovery MimeKit does not do.
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
            // An encoded name before a bare addr-spec ("=?..?= a@x.org"): MimeKit parses neither form, so split at the last space.
            var space = decoded.LastIndexOfAny([' ', '\t']);
            if (space > 0 && !decoded.Contains("=?", StringComparison.Ordinal)
                && InternetAddressList.TryParse(LooseOptions, decoded[(space + 1)..], out var bare)
                && bare is [MailboxAddress { Name: null or "" } spec])
            {
                target.Add(new MailboxAddress(decoded[..space].Trim(), spec.Address));
                return true;
            }

            target.Add(RawMailbox.Create(item.Replace("\r", "").Replace("\n", " ").Trim()));
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

    /// <summary>
    /// Marks an undecodable raw item kept as a mailbox: participant has Address only. <see cref="Raw"/> holds the text, so an
    /// item that is no addr-spec (MimeKit's constructor throws on it) is kept as is instead of throwing.
    /// </summary>
    private sealed class RawMailbox(string raw) : MailboxAddress("", "")
    {
        public string Raw { get; } = raw;

        /// <summary>MimeKit's addr-spec of the item (comments dropped) when it is one, else the item text.</summary>
        public static RawMailbox Create(string item)
        {
            try
            {
                return new RawMailbox(new MailboxAddress("", item).Address);
            }
            catch (Exception ex) when (EmlExtractor.IsContentFailure(ex))
            {
                return new RawMailbox(item);
            }
        }
    }

    private static IEnumerable<string> SplitItems(string raw)
    {
        var start = 0;
        var quoted = false;
        var comment = 0;
        var angle = false;
        var group = false;
        var stray = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && (quoted || comment > 0))
                i++;
            else if (quoted)
                quoted = c != '"';
            else if (comment > 0)
                comment += c == '(' ? 1 : c == ')' ? -1 : 0;
            else if (c == '=' && EncodedWordEnd(raw, i) is var end and > 0)
                i = end;
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
            else if (c == ':' && !stray)
                group = true;
            else if (c == ';' && group)
            {
                group = false;
                if (raw[start..(i + 1)].Trim() is { Length: > 0 } g)
                    yield return g;
                start = i + 1;
            }
            else if (c == ';')
            {
                // Stray ';' outside a group: like MimeKit's To parse, keep the item before it and drop the rest up to the
                // next ',' (a group start in the dropped part is ignored too).
                if (!stray && raw[start..i].Trim() is { Length: > 0 } item)
                    yield return item;
                stray = true;
                start = i + 1;
            }
            else if (c == ',' && !group)
            {
                if (!stray && raw[start..i].Trim() is { Length: > 0 } item)
                    yield return item;
                stray = false;
                start = i + 1;
            }
        }

        if (!stray && raw[start..].Trim().Trim(',') is { Length: > 0 } last)
            yield return last;
    }

    /// <summary>Index of the closing '=' when an <c>=?charset?enc?text?=</c> token starts at <paramref name="i"/>, else -1.</summary>
    private static int EncodedWordEnd(string raw, int i)
    {
        if (i + 1 >= raw.Length || raw[i + 1] != '?')
            return -1;
        var marks = 0;
        for (var j = i + 2; j < raw.Length && !char.IsWhiteSpace(raw[j]); j++)
        {
            if (raw[j] != '?')
                continue;
            if (marks == 2 && j + 1 < raw.Length && raw[j + 1] == '=')
                return j + 1;
            if (++marks > 2)
                return -1;
        }

        return -1;
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
        if (mailbox is RawMailbox raw)
            return new EmailParticipant("", raw.Raw, "", "");
        return at < 0
            ? new EmailParticipant(mailbox.Name ?? "", address, address, "")
            : new EmailParticipant(mailbox.Name ?? "", address, address[..at], address[(at + 1)..]);
    }

    private static string? Join(InternetAddressList list)
        => list.Count == 0 ? null : string.Join("; ", list.Select(FormatAddress));

    private static string FormatAddress(InternetAddress address) => address switch
    {
        RawMailbox raw => raw.Raw,
        MailboxAddress mailbox => string.IsNullOrEmpty(mailbox.Name)
            ? (mailbox.Address ?? "")
            : $"{mailbox.Name} <{mailbox.Address}>",
        GroupAddress group => $"{group.Name}: {string.Join("; ", group.Members.Select(FormatAddress))};",
        _ => address.ToString()
    };
}
