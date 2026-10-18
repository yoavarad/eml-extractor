using System.Globalization;
using System.Text.Json;

namespace EmlExtraction.Bench.Sweep;

/// <summary>
/// <c>compare --baseline &lt;file&gt; --current &lt;file&gt; [--threshold 0.2]</c>. Warn-only: exits 0 whether or not
/// it warns or the baseline is not comparable. Exit 2 only for bad arguments or unreadable files.
/// </summary>
internal static class CompareCommand
{
    private const string Usage = "Usage: compare --baseline <file> --current <file> [--threshold 0.2]";

    public static int Run(string[] args)
    {
        string? baselinePath = null, currentPath = null;
        var threshold = BaselineCompare.DefaultThreshold;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
                return Fail($"Missing value for {args[i]}. {Usage}");
            var value = args[i + 1];
            switch (args[i])
            {
                case "--baseline": baselinePath = value; break;
                case "--current": currentPath = value; break;
                case "--threshold" when double.TryParse(value, CultureInfo.InvariantCulture, out var t) && t > 0 && t < 1: threshold = t; break;
                default: return Fail(Usage);
            }
        }

        if (baselinePath is null || currentPath is null)
            return Fail(Usage);

        SweepReport baseline, current;
        try
        {
            baseline = Load(baselinePath);
            current = Load(currentPath);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
        {
            return Fail(ex.Message);
        }

        var result = BaselineCompare.Compare(baseline, current, threshold);
        if (!result.Comparable)
        {
            Console.WriteLine($"baseline not comparable: {result.NotComparableField}");
            return 0;
        }

        var github = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
        foreach (var w in result.Warnings)
            Console.WriteLine(github ? $"::warning title=perf regression::{w}" : $"WARNING: {w}");
        if (result.MetricsChecked == 0)
        {
            Console.WriteLine("compare: nothing compared (no set/DOP measured in both reports)");
            return 0;
        }

        Console.WriteLine(result.Warnings.Count == 0
            ? $"compare: no regression beyond {threshold:P0} ({result.MetricsChecked} metrics checked)"
            : $"compare: {result.Warnings.Count} warning(s) (warn-only, exit 0)");
        return 0;
    }

    private static SweepReport Load(string path)
    {
        var report = JsonSerializer.Deserialize<SweepReport>(File.ReadAllText(path), SweepReport.Json)
            ?? throw new InvalidDataException($"{path} is empty.");
        if (report.SchemaVersion != SweepReport.CurrentSchemaVersion)
            throw new InvalidDataException($"{path}: schemaVersion {report.SchemaVersion}, expected {SweepReport.CurrentSchemaVersion}.");
        return report;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"ERROR: {message}");
        return 2;
    }
}
