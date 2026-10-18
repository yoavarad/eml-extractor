namespace EmlExtraction.Bench.Sweep;

/// <summary>Pure parsers for OS CPU descriptions (ADR-015 hardware block). Linked into the test project.</summary>
internal static class CpuInfoParser
{
    /// <summary>Trims and collapses whitespace; null when empty or a placeholder such as "Unknown processor".</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var collapsed = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Equals("Unknown processor", StringComparison.OrdinalIgnoreCase)
            || collapsed.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : collapsed;
    }

    /// <summary>First <c>model name</c> value from Linux <c>/proc/cpuinfo</c>.</summary>
    public static string? ParseProcCpuinfoModel(string text)
    {
        foreach (var (key, value) in Fields(text))
        {
            if (key == "model name")
                return Normalize(value);
        }

        return null;
    }

    /// <summary>Distinct (physical id, core id) pairs from Linux <c>/proc/cpuinfo</c>; null when core ids are absent.</summary>
    public static int? ParseProcCpuinfoPhysicalCores(string text)
    {
        var cores = new HashSet<(string, string)>();
        string physical = "0";
        foreach (var (key, value) in Fields(text))
        {
            if (key == "processor")
                physical = "0";
            else if (key == "physical id")
                physical = value;
            else if (key == "core id")
                cores.Add((physical, value));
        }

        return cores.Count == 0 ? null : cores.Count;
    }

    /// <summary>Output of <c>sysctl -n &lt;key&gt;</c> (macOS), normalized.</summary>
    public static string? ParseSysctlValue(string text) => Normalize(text);

    private static IEnumerable<(string Key, string Value)> Fields(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;
            yield return (line[..colon].Trim(), line[(colon + 1)..].Trim());
        }
    }
}
