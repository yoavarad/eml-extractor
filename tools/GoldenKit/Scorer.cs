using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmlExtraction;

namespace GoldenKit;

internal static class Scorer
{
    public static int Run(string repoRoot)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var detector = new EmlDetector();
        var extractor = new EmlExtractor();
        var files = Discover(repoRoot).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<CaseScore>();

        foreach (var expectedPath in files)
        {
            var spec = JsonSerializer.Deserialize<GoldenSpecFile>(File.ReadAllText(expectedPath), Json.Options)
                       ?? new GoldenSpecFile { Id = expectedPath };
            if (!seen.Add(spec.Id))
                continue;
            var emlPath = Path.Combine(Path.GetDirectoryName(expectedPath)!, "input.eml");
            var score = Evaluate(spec, File.ReadAllBytes(emlPath), detector, extractor);
            results.Add(score);
        }

        var possible = results.Sum(r => r.Possible);
        var earned = results.Sum(r => r.Earned);
        var passed = results.Count(r => r.Failed.Count == 0);
        var pct = possible == 0 ? 0 : 100.0 * earned / possible;
        var casePct = results.Count == 0 ? 0 : 100.0 * passed / results.Count;

        var byTag = new Dictionary<string, (int Pass, int Total)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            foreach (var tag in r.Tags)
            {
                byTag.TryGetValue(tag, out var t);
                byTag[tag] = (t.Pass + (r.Failed.Count == 0 ? 1 : 0), t.Total + 1);
            }
        }

        var report = new StringBuilder();
        report.AppendLine($"GOLDEN SCORE");
        report.AppendLine($"cases: {passed}/{results.Count} passed ({casePct:F1}% cases)");
        report.AppendLine($"checks: {earned}/{possible} ({pct:F1}% assertions)");
        report.AppendLine();
        report.AppendLine("By tag (case pass rate):");
        foreach (var kv in byTag.OrderBy(k => k.Key))
            report.AppendLine($"  {kv.Key,-28} {kv.Value.Pass,4}/{kv.Value.Total,-4} {100.0 * kv.Value.Pass / kv.Value.Total,5:F1}%");

        var fails = results.Where(r => r.Failed.Count > 0).ToList();
        report.AppendLine();
        report.AppendLine($"Failures: {fails.Count}");
        foreach (var f in fails.Take(80))
            report.AppendLine($"  {f.Id} [{string.Join(",", f.Tags.Take(6))}] {string.Join(" | ", f.Failed.Take(4))}");
        if (fails.Count > 80)
            report.AppendLine($"  ... {fails.Count - 80} more");

        var outDir = Path.Combine(repoRoot, "docs");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "golden-score.txt"), report.ToString());
        File.WriteAllText(Path.Combine(outDir, "golden-score.json"), JsonSerializer.Serialize(new
        {
            cases_passed = passed,
            cases_total = results.Count,
            case_pass_pct = casePct,
            checks_earned = earned,
            checks_possible = possible,
            check_pct = pct,
            tags = byTag.ToDictionary(k => k.Key, k => new { k.Value.Pass, k.Value.Total }),
            failures = fails.Select(f => new { f.Id, f.Tags, f.Failed })
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.Write(report.ToString());
        return fails.Count == 0 ? 0 : 1;
    }

    private static CaseScore Evaluate(GoldenSpecFile spec, byte[] eml, EmlDetector detector, EmlExtractor extractor)
    {
        var s = new CaseScore { Id = spec.Id, Tags = spec.Tags };
        Check(s, "detect", spec.IsEml == detector.IsEml(eml));
        if (!spec.IsEml)
            return s;

        var result = extractor.Extract(eml);
        ScoreResult(s, result, spec.Expect, extractor, depth: 0);
        return s;
    }

    private static void ScoreResult(CaseScore s, EmlExtractionResult result, GoldenExpect expect, EmlExtractor extractor, int depth)
    {
        var internalError = result.Warnings.FirstOrDefault(w => w.StartsWith("internal_error", StringComparison.Ordinal));
        Check(s, "no_internal_error", internalError is null, internalError);
        foreach (var (key, field) in expect.Metadata)
        {
            result.Metadata.TryGetValue(key, out var actual);
            Check(s, $"metadata.{key}", FieldOk(actual, field));
        }

        if (expect.XFields is not null)
            Check(s, "xfields", FieldOk(result.XFields, expect.XFields));
        if (expect.MainContent is not null)
            Check(s, "main_content", FieldOk(result.MainContent ?? "", expect.MainContent));
        if (expect.MainContentMimeType is not null)
            Check(s, "main_content_mime", string.Equals(expect.MainContentMimeType, result.MainContentMimeType, StringComparison.OrdinalIgnoreCase));
        if (expect.AttachmentCount is int count)
            Check(s, "attachment_count", result.Attachments.Count == count, $"got {result.Attachments.Count} expected {count}");

        foreach (var wanted in expect.Attachments)
        {
            var match = result.Attachments.FirstOrDefault(a =>
                (wanted.Sha256 is string wantSha
                 && Convert.ToHexString(SHA256.HashData(a.Data)).Equals(wantSha, StringComparison.OrdinalIgnoreCase))
                || (wanted.Name is not null && a.Name.Equals(wanted.Name, StringComparison.OrdinalIgnoreCase))
                || (wanted.NameContains is not null && a.Name.Contains(wanted.NameContains, StringComparison.OrdinalIgnoreCase)));
            Check(s, $"attach:{wanted.Name ?? wanted.NameContains}", match is not null);
            if (match is null)
                continue;
            if (wanted.Size is long size)
                Check(s, $"attach.size:{match.Name}", match.Size == size, $"got {match.Size}");
            if (wanted.Sha256 is string sha)
            {
                var actual = Convert.ToHexString(SHA256.HashData(match.Data));
                Check(s, $"attach.sha:{match.Name}", actual.Equals(sha, StringComparison.OrdinalIgnoreCase));
            }

            if (wanted.Disposition is string disp)
                Check(s, $"attach.disp:{match.Name}", string.Equals(match.Disposition, disp, StringComparison.OrdinalIgnoreCase), $"got {match.Disposition}");
            if (wanted.FullPath is string path)
                Check(s, $"attach.path:{match.Name}", match.FullPath == path, $"got {match.FullPath}");
            if (wanted.Reextract is not null && depth < 2)
                ScoreResult(s, extractor.Extract(match.Data), wanted.Reextract, extractor, depth + 1);
        }
    }

    private static bool FieldOk(string? actual, GoldenField field)
    {
        actual ??= "";
        if (field.Eq is not null && actual != field.Eq)
            return false;
        if (field.Contains.Any(p => !actual.Contains(p, StringComparison.Ordinal)))
            return false;
        if (field.NotContains.Any(p => actual.Contains(p, StringComparison.Ordinal)))
            return false;
        return true;
    }

    private static void Check(CaseScore s, string name, bool ok, string? detail = null)
    {
        s.Possible++;
        if (ok)
        {
            s.Earned++;
            return;
        }

        s.Failed.Add(detail is null ? name : $"{name} ({detail})");
    }

    private static IEnumerable<string> Discover(string repoRoot)
    {
        foreach (var root in new[]
                 {
                     Path.Combine(repoRoot, "corpus", "golden"),
                     Path.Combine(repoRoot, "corpus", "internal", "golden")
                 })
        {
            if (!Directory.Exists(root))
                continue;
            foreach (var file in Directory.EnumerateFiles(root, "expected.json", SearchOption.AllDirectories))
                yield return file;
        }
    }

    private sealed class CaseScore
    {
        public string Id { get; set; } = "";
        public string[] Tags { get; set; } = [];
        public int Earned { get; set; }
        public int Possible { get; set; }
        public List<string> Failed { get; } = [];
    }
}
