using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EmlExtraction;
using MimeKit;

namespace GoldenKit;

internal static class Probe
{
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "golden", "bulk", "seed", "downloads"
    };

    private static readonly HashSet<string> SkipExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".md", ".cs", ".dll", ".pdb", ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".zip",
        ".bz2", ".gz", ".tgz", ".tar", ".exe", ".so", ".dylib", ".list", ".csproj",
        ".tnef", ".p7s", ".p7m", ".asc", ".pem", ".pfx", ".key", ".cfg", ".crt", ".cnf",
        ".sh", ".db", ".gpg", ".sec", ".pub", ".html", ".xhtml", ".tokens", ".pbm"
    };

    public static int Run(string repo, string? relative)
    {
        var root = Path.GetFullPath(Path.Combine(repo, relative ?? Path.Combine("corpus", "internal")));
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"missing: {root}");
            return 1;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var detector = new EmlDetector();
        var extractor = new EmlExtractor();
        var files = Discover(root).ToList();
        var stats = new Stats();
        var sw = Stopwatch.StartNew();
        var i = 0;

        Console.WriteLine($"probe {files.Count} files under {root}");
        foreach (var path in files)
        {
            i++;
            if (i % 500 == 0)
                Console.WriteLine($"  … {i}/{files.Count} messages={stats.Messages} warn={stats.Warned} throw={stats.Thrown} {sw.Elapsed:mm\\:ss}");

            ProbeFile(path, root, detector, extractor, stats);
        }

        sw.Stop();
        var reportPath = Path.Combine(root, "probe-report.json");
        File.WriteAllText(reportPath, stats.ToJson(root, files.Count, sw.Elapsed));
        Console.WriteLine(stats.ToText(root, files.Count, sw.Elapsed));
        Console.WriteLine($"report: {reportPath}");
        return stats.Thrown > 0 ? 1 : 0;
    }

    private static IEnumerable<string> Discover(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith('.'))
                continue;
            if (name.Equals("cmds", StringComparison.OrdinalIgnoreCase)
                || name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase)
                || name.Equals("SOURCES.txt", StringComparison.OrdinalIgnoreCase)
                || name.Equals("probe-report.json", StringComparison.OrdinalIgnoreCase)
                || name.Equals("probe-failures.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            var ext = Path.GetExtension(path);
            if (SkipExt.Contains(ext))
                continue;

            var dir = Path.GetDirectoryName(path);
            if (dir is not null && dir.Split(Path.DirectorySeparatorChar).Any(SkipDirNames.Contains))
                continue;

            yield return path;
        }
    }

    private static void ProbeFile(string path, string root, EmlDetector detector, EmlExtractor extractor, Stats stats)
    {
        var rel = Path.GetRelativePath(root, path);
        FileInfo info;
        try
        {
            info = new FileInfo(path);
        }
        catch
        {
            return;
        }

        if (info.Length == 0)
        {
            stats.EmptyFiles++;
            return;
        }

        if (info.Length > 40 * 1024 * 1024)
        {
            stats.SkippedLarge++;
            stats.Note("skipped_large", rel, info.Length, "file > 40MB");
            return;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            stats.ReadFail++;
            stats.Note("read_fail", rel, info.Length, ex.Message);
            return;
        }

        stats.Files++;
        stats.Bytes += bytes.Length;

        if (IsMbox(bytes, path))
        {
            ProbeMbox(bytes, rel, extractor, stats);
            return;
        }

        ProbeMessage(bytes, rel, 0, detector, extractor, stats);
    }

    private static bool IsMbox(byte[] bytes, string path)
    {
        if (path.EndsWith(".mbox", StringComparison.OrdinalIgnoreCase))
            return true;
        if (bytes.Length < 5 || bytes[0] != (byte)'F' || bytes[1] != (byte)'r' || bytes[2] != (byte)'o' || bytes[3] != (byte)'m' || bytes[4] != (byte)' ')
            return false;

        var extra = 0;
        for (var i = 1; i < bytes.Length - 5; i++)
        {
            if (bytes[i] != (byte)'\n')
                continue;
            if (bytes[i + 1] == (byte)'F' && bytes[i + 2] == (byte)'r' && bytes[i + 3] == (byte)'o' && bytes[i + 4] == (byte)'m' && bytes[i + 5] == (byte)' ')
            {
                extra++;
                if (extra >= 2)
                    return true;
            }
        }

        return false;
    }

    private static void ProbeMbox(byte[] bytes, string rel, EmlExtractor extractor, Stats stats)
    {
        stats.MboxFiles++;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var parser = new MimeParser(stream, MimeFormat.Mbox);
            var n = 0;
            while (!parser.IsEndOfStream)
            {
                MimeMessage message;
                try
                {
                    message = parser.ParseMessage();
                }
                catch (Exception ex)
                {
                    stats.Thrown++;
                    stats.Note("mbox_parse_throw", $"{rel}#{n}", 0, ex.GetType().Name + ": " + ex.Message);
                    break;
                }

                n++;
                ProbeParsed(message, $"{rel}#{n}", (int)Math.Min(int.MaxValue, message.Headers.Count), extractor, stats);
            }

            if (n == 0)
                stats.Note("mbox_empty", rel, bytes.Length, "no messages");
        }
        catch (Exception ex)
        {
            stats.Thrown++;
            stats.Note("mbox_throw", rel, bytes.Length, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProbeMessage(byte[] bytes, string rel, int index, EmlDetector detector, EmlExtractor extractor, Stats stats)
    {
        var isEml = false;
        try
        {
            isEml = detector.IsEml(bytes);
        }
        catch (Exception ex)
        {
            stats.Thrown++;
            stats.Note("detect_throw", rel, bytes.Length, ex.GetType().Name + ": " + ex.Message);
            return;
        }

        stats.Detected += isEml ? 1 : 0;
        stats.NotDetected += isEml ? 0 : 1;
        if (!isEml)
        {
            stats.Bump("not_detected_ext:" + (Path.GetExtension(rel).ToLowerInvariant() is { Length: > 0 } e ? e : "(none)"));
            return;
        }

        EmlExtractionResult result;
        try
        {
            result = extractor.Extract(bytes);
        }
        catch (Exception ex)
        {
            stats.Thrown++;
            stats.Note("extract_throw", rel, bytes.Length, ex.GetType().Name + ": " + ex.Message);
            return;
        }

        Observe(result, rel, bytes.Length, isEml, stats);
        _ = index;
    }

    private static void ProbeParsed(MimeMessage message, string label, int _, EmlExtractor extractor, Stats stats)
    {
        stats.Detected++;
        EmlExtractionResult result;
        try
        {
            result = extractor.Extract(message);
        }
        catch (Exception ex)
        {
            stats.Thrown++;
            stats.Note("extract_throw", label, 0, ex.GetType().Name + ": " + ex.Message);
            return;
        }

        Observe(result, label, 0, true, stats);
    }

    private static void Observe(EmlExtractionResult result, string label, int size, bool detected, Stats stats)
    {
        stats.Messages++;
        if (result.Warnings.Count > 0)
        {
            stats.Warned++;
            foreach (var w in result.Warnings)
            {
                var kind = w.Split(':', 2)[0];
                stats.Bump("warn:" + kind);
                if (stats.Samples.Count < 80)
                    stats.Note("warning", label, size, w);
            }
        }

        if (result.Warnings.Any(w => w.StartsWith("parse_failed", StringComparison.Ordinal)))
            stats.ParseFailed++;

        stats.Headers += result.Metadata.Count;
        if (result.Metadata.Count > stats.MaxHeaders)
        {
            stats.MaxHeaders = result.Metadata.Count;
            stats.MaxHeadersLabel = label;
        }

        var xCount = string.IsNullOrEmpty(result.Metadata["xfields"]) ? 0 : result.Metadata["xfields"].Split('\n').Length;
        stats.XFields += xCount;
        if (xCount > stats.MaxXFields)
        {
            stats.MaxXFields = xCount;
            stats.MaxXFieldsLabel = label;
        }

        if (result.MainContent is { Length: > 0 })
            stats.HasBody++;
        else
            stats.EmptyBody++;

        if (result.MainContentMimeType is not null)
            stats.Bump("body:" + result.MainContentMimeType.ToLowerInvariant());

        if (result.Attachments.Count == 0)
            stats.NoAttach++;
        else
            stats.WithAttach++;

        stats.AttachCount += result.Attachments.Count;
        var hasCid = false;
        var hasTnefLeftover = false;
        foreach (var a in result.Attachments)
        {
            stats.AttachBytes += a.Size;
            if (a.Size > stats.MaxAttach)
            {
                stats.MaxAttach = a.Size;
                stats.MaxAttachLabel = $"{label} {a.Name}";
            }

            var ext = Path.GetExtension(a.Name).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                ext = "(none)";
            stats.Bump("att_ext:" + ext);

            if (!string.IsNullOrEmpty(a.ContentId))
            {
                hasCid = true;
                stats.CidParts++;
            }

            if (a.Name.Equals("winmail.dat", StringComparison.OrdinalIgnoreCase)
                || a.Name.EndsWith(".tnef", StringComparison.OrdinalIgnoreCase))
                hasTnefLeftover = true;

            if (LooksOffice(ext))
                stats.Office++;
            if (ext is ".eml" || (a.Name.Contains("rfc822", StringComparison.OrdinalIgnoreCase)))
                stats.NestedMail++;
        }

        if (hasCid)
            stats.CidMessages++;
        if (hasTnefLeftover)
            stats.TnefOpaque++;

        if (result.Metadata.Keys.Any(k => k.Contains("ms-exchange", StringComparison.Ordinal)))
            stats.ExchangeHeaders++;
        if (result.Metadata.TryGetValue("content-type", out var ct) && ct.Contains("tnef", StringComparison.OrdinalIgnoreCase))
            stats.TnefContentType++;
        if (result.Metadata["xfields"].Contains("winmail", StringComparison.OrdinalIgnoreCase)
            || result.Metadata.TryGetValue("x-ms-tnef-correlator", out _))
            stats.TnefHeaders++;

        if (detected && result.Warnings.Any(w => w.StartsWith("parse_failed", StringComparison.Ordinal)))
            stats.DetectedButParseFailed++;
    }

    private static bool LooksOffice(string ext)
        => ext is ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx"
            or ".odt" or ".ods" or ".odp" or ".rtf" or ".pdf";
}
