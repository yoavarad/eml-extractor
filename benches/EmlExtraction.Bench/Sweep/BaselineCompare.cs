using System.Globalization;
using System.Text.RegularExpressions;

namespace EmlExtraction.Bench.Sweep;

/// <summary>Outcome of comparing a sweep against a baseline. Never a failure: warn-only by design.</summary>
internal sealed record CompareResult(bool Comparable, string? NotComparableField, IReadOnlyList<string> Warnings, int MetricsChecked);

/// <summary>
/// Baseline comparability check and warn-only regression rule (ADR-015). Pure; linked into the test project.
/// Only compares when CPU model, logical cores, runtime major.minor and GC mode all match.
/// </summary>
internal static partial class BaselineCompare
{
    /// <summary>Provisional regression threshold (20%).</summary>
    public const double DefaultThreshold = 0.20;

    public static CompareResult Compare(SweepReport baseline, SweepReport current, double threshold = DefaultThreshold)
    {
        var b = baseline.Hardware;
        var c = current.Hardware;
        if (!string.Equals(b.CpuModel, c.CpuModel, StringComparison.Ordinal))
            return NotComparable("cpuModel");
        if (b.LogicalCores != c.LogicalCores)
            return NotComparable("logicalCores");
        if (!string.Equals(b.OsArchitecture, c.OsArchitecture, StringComparison.Ordinal))
            return NotComparable("osArchitecture");
        if (!string.Equals(b.Os, c.Os, StringComparison.Ordinal))
            return NotComparable("os");
        if (RuntimeMajorMinor(b.Runtime) != RuntimeMajorMinor(c.Runtime))
            return NotComparable("runtime");

        var warnings = new List<string>();
        var checkedCount = 0;
        foreach (var curRun in current.Runs)
        {
            var baseRun = baseline.Runs.FirstOrDefault(r => r.GcMode == curRun.GcMode);
            if (baseRun is null)
                return NotComparable("gcMode");
            foreach (var curSet in curRun.Sets)
            {
                var baseSet = baseRun.Sets.FirstOrDefault(s => s.Name == curSet.Name);
                if (baseSet is null || baseSet.Status != "measured" || curSet.Status != "measured")
                    continue;
                var label = $"{curSet.Name}/{curRun.GcMode}";
                checkedCount += CheckPoints(label, baseSet.Extract, curSet.Extract, c.LogicalCores, threshold, warnings);
            }
        }

        return new CompareResult(true, null, warnings, checkedCount);
    }

    private static int CheckPoints(string label, IReadOnlyList<SweepPoint> baseline, IReadOnlyList<SweepPoint> current, int cores, double threshold, List<string> warnings)
    {
        var n = 0;
        var b1 = At(baseline, 1);
        var c1 = At(current, 1);
        var bn = At(baseline, cores);
        var cn = At(current, cores);
        if (b1 is not null && c1 is not null)
        {
            n++;
            Drop(label, "T(1)", b1.FilesPerSec, c1.FilesPerSec, threshold, warnings);
        }

        if (bn is not null && cn is not null)
        {
            n += 3;
            Drop(label, $"T({cores})", bn.FilesPerSec, cn.FilesPerSec, threshold, warnings);
            Rise(label, "A", bn.AllocBytesPerInputByte, cn.AllocBytesPerInputByte, threshold, warnings);
            Rise(label, $"P({cores})", bn.PeakWorkingSetBytes, cn.PeakWorkingSetBytes, threshold, warnings);
        }

        return n;
    }

    private static SweepPoint? At(IReadOnlyList<SweepPoint> points, int dop) => points.FirstOrDefault(p => p.Dop == dop);

    private static void Drop(string label, string metric, double baseline, double current, double threshold, List<string> warnings)
    {
        if (baseline > 0 && current < baseline * (1 - threshold))
            warnings.Add($"{label} {metric} dropped {Pct(baseline, current)}: {Fmt(baseline)} -> {Fmt(current)}");
    }

    private static void Rise(string label, string metric, double baseline, double current, double threshold, List<string> warnings)
    {
        if (baseline > 0 && current > baseline * (1 + threshold))
            warnings.Add($"{label} {metric} rose {Pct(baseline, current)}: {Fmt(baseline)} -> {Fmt(current)}");
    }

    private static string Pct(double baseline, double current) =>
        (Math.Abs(current - baseline) / baseline * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static CompareResult NotComparable(string field) => new(false, field, [], 0);

    /// <summary>"major.minor" of a runtime string such as ".NET 10.0.12"; the raw string when none is found.</summary>
    internal static string RuntimeMajorMinor(string runtime)
    {
        var m = VersionRegex().Match(runtime);
        return m.Success ? m.Value : runtime;
    }

    [GeneratedRegex(@"\d+\.\d+")]
    private static partial Regex VersionRegex();
}
