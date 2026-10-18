using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace GoldenKit;

internal sealed class Stats
{
    public int Files, Messages, Detected, NotDetected, MboxFiles, EmptyFiles, SkippedLarge, ReadFail;
    public int Warned, Thrown, ParseFailed, DetectedButParseFailed;
    public int HasBody, EmptyBody, NoAttach, WithAttach, CidMessages, CidParts;
    public int TnefOpaque, TnefHeaders, TnefContentType, ExchangeHeaders, Office, NestedMail;
    public int Headers, XFields, AttachCount;
    public long Bytes, AttachBytes, MaxAttach;
    public int MaxHeaders, MaxXFields;
    public string MaxHeadersLabel = "", MaxXFieldsLabel = "", MaxAttachLabel = "";
    public ConcurrentDictionary<string, int> Counts { get; } = new();
    public List<string> Samples { get; } = [];

    public void Bump(string key) => Counts.AddOrUpdate(key, 1, (_, n) => n + 1);

    public void Note(string kind, string label, long size, string detail)
    {
        lock (Samples)
        {
            if (Samples.Count >= 120)
                return;
            Samples.Add($"{kind}\t{label}\t{size}\t{detail}");
        }
    }

    public string ToText(string root, int fileCount, TimeSpan elapsed)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"root={root}");
        sb.AppendLine($"files_seen={fileCount} files_read={Files} mbox={MboxFiles} empty={EmptyFiles} skipped_large={SkippedLarge} read_fail={ReadFail}");
        sb.AppendLine($"messages={Messages} detected={Detected} not_detected={NotDetected} parse_failed={ParseFailed} warned={Warned} thrown={Thrown} elapsed={elapsed:hh\\:mm\\:ss} rate={(elapsed.TotalSeconds > 0 ? Messages / elapsed.TotalSeconds : 0):F1}/s");
        sb.AppendLine($"body={HasBody} empty_body={EmptyBody} with_attach={WithAttach} no_attach={NoAttach} cid_msgs={CidMessages} cid_parts={CidParts}");
        sb.AppendLine($"tnef_headers={TnefHeaders} tnef_left_as_winmail={TnefOpaque} exchange_hdr={ExchangeHeaders} office={Office} nested_rfc822={NestedMail}");
        sb.AppendLine($"max_headers={MaxHeaders} ({MaxHeadersLabel}) max_xfields={MaxXFields} ({MaxXFieldsLabel}) max_attach={MaxAttach} ({MaxAttachLabel})");
        sb.AppendLine("top tags:");
        foreach (var kv in Counts.OrderByDescending(k => k.Value).Take(40))
            sb.AppendLine($"  {kv.Value,7}  {kv.Key}");
        if (Samples.Count > 0)
        {
            sb.AppendLine("samples:");
            foreach (var s in Samples.Take(40))
                sb.AppendLine("  " + s);
        }

        return sb.ToString();
    }

    public string ToJson(string root, int fileCount, TimeSpan elapsed)
        => JsonSerializer.Serialize(new
        {
            root,
            fileCount,
            Files,
            Messages,
            Detected,
            NotDetected,
            MboxFiles,
            Warned,
            Thrown,
            ParseFailed,
            HasBody,
            EmptyBody,
            WithAttach,
            NoAttach,
            CidMessages,
            CidParts,
            TnefHeaders,
            TnefOpaque,
            ExchangeHeaders,
            Office,
            NestedMail,
            MaxHeaders,
            MaxHeadersLabel,
            MaxXFields,
            MaxXFieldsLabel,
            MaxAttach,
            MaxAttachLabel,
            seconds = elapsed.TotalSeconds,
            counts = Counts.OrderByDescending(k => k.Value).ToDictionary(k => k.Key, k => k.Value),
            samples = Samples
        }, new JsonSerializerOptions { WriteIndented = true });
}
