using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EmlExtraction.Bench.Sweep;

// JSON schema v1, documented in docs/perf-results.md ("DOP sweep JSON schema"). Consumed by the baseline task:
// add fields freely, but bump SchemaVersion on any rename, removal, or change of meaning.

internal sealed record HardwareBlock(
    string CpuModel,
    int PhysicalCores,
    int LogicalCores,
    long TotalMemoryBytes,
    string Os,
    string OsArchitecture,
    string Runtime,
    string TargetFramework,
    string Date,
    string GitCommit);

internal sealed record SweepParameters(
    int WarmupMs,
    int WindowMs,
    int MinPasses,
    int SampleIntervalMs,
    int MaxDop,
    string Set,
    string Corpus);

internal sealed record SweepPoint(
    int Dop,
    double FilesPerSec,
    double P50Ms,
    double P95Ms,
    long PeakWorkingSetBytes,
    double AllocBytesPerInputByte,
    double? Efficiency,
    long Messages,
    double ElapsedSec,
    double Passes);

internal sealed record SetResult(
    string Name,
    string Status,
    string? SkipReason,
    int Messages,
    long InputBytes,
    IReadOnlyList<SweepPoint> Extract,
    IReadOnlyList<SweepPoint> Detect);

internal sealed record GcRun(string GcMode, IReadOnlyList<SetResult> Sets);

internal sealed record SweepReport(int SchemaVersion, HardwareBlock Hardware, SweepParameters Parameters, IReadOnlyList<GcRun> Runs)
{
    public const int CurrentSchemaVersion = 1;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string ToMarkdown()
    {
        var md = new StringBuilder();
        md.AppendLine("# DOP sweep");
        md.AppendLine();
        md.AppendLine("| Hardware | |");
        md.AppendLine("| --- | --- |");
        md.AppendLine($"| CPU | {Hardware.CpuModel} |");
        md.AppendLine($"| Cores (physical / logical) | {Hardware.PhysicalCores} / {Hardware.LogicalCores} |");
        md.AppendLine($"| RAM | {Hardware.TotalMemoryBytes / (1024.0 * 1024 * 1024):F1} GiB |");
        md.AppendLine($"| OS | {Hardware.Os} ({Hardware.OsArchitecture}) |");
        md.AppendLine($"| Runtime / TFM | {Hardware.Runtime} / {Hardware.TargetFramework} |");
        md.AppendLine($"| Date | {Hardware.Date} |");
        md.AppendLine($"| Commit | {Hardware.GitCommit} |");
        md.AppendLine();
        md.AppendLine(
            $"Parameters: warm-up {Parameters.WarmupMs} ms, window >= {Parameters.WindowMs} ms and >= {Parameters.MinPasses} passes, " +
            $"working set sampled every {Parameters.SampleIntervalMs} ms, n = 1..{Parameters.MaxDop}, set `{Parameters.Set}`, corpus `{Parameters.Corpus}`.");

        foreach (var run in Runs)
        {
            foreach (var set in run.Sets)
            {
                md.AppendLine();
                md.AppendLine($"## {set.Name} - {run.GcMode} GC");
                md.AppendLine();
                if (set.Status != "measured")
                {
                    md.AppendLine($"SKIPPED: {set.SkipReason}");
                    continue;
                }

                md.AppendLine($"{set.Messages} messages, {set.InputBytes} bytes.");
                AppendTable(md, "Extract", set.Extract);
                AppendTable(md, "Detect", set.Detect);
            }
        }

        return md.ToString();
    }

    public static string Header() =>
        $"{"n",4} {"T(n) f/s",12} {"P50 ms",9} {"P95 ms",9} {"P(n) MB",9} {"A B/B",8} {"E(n)",6}";

    public static string Row(SweepPoint p) => string.Create(
        CultureInfo.InvariantCulture,
        $"{p.Dop,4} {p.FilesPerSec,12:F1} {p.P50Ms,9:F3} {p.P95Ms,9:F3} {p.PeakWorkingSetBytes / (1024.0 * 1024),9:F1} {p.AllocBytesPerInputByte,8:F2} {Eff(p.Efficiency),6}");

    /// <summary>E(n) text; "n/a" when the run has no DOP 1 baseline.</summary>
    public static string Eff(double? e) => e is null ? "n/a" : e.Value.ToString("F3", CultureInfo.InvariantCulture);

    private static void AppendTable(StringBuilder md, string title, IReadOnlyList<SweepPoint> points)
    {
        md.AppendLine();
        md.AppendLine($"### {title}");
        md.AppendLine();
        md.AppendLine("| n | T(n) files/s | P50 ms | P95 ms | P(n) MB | A B/B | E(n) |");
        md.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var p in points)
        {
            md.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {p.Dop} | {p.FilesPerSec:F1} | {p.P50Ms:F3} | {p.P95Ms:F3} | {p.PeakWorkingSetBytes / (1024.0 * 1024):F1} | {p.AllocBytesPerInputByte:F2} | {Eff(p.Efficiency)} |"));
        }
    }
}
