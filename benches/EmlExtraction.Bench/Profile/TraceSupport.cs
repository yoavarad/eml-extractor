namespace EmlExtraction.Bench.Profile;

/// <summary>
/// Child-side support for <c>profile-trace</c>: a stdin handshake (the parent attaches dotnet-trace/counters/gcdump
/// to this process, then releases it) and per-run GC counters (collection counts, total pause).
/// </summary>
internal sealed class TraceSupport
{
    private const string Marker = "EML-HANDSHAKE ";
    private readonly bool _enabled;
    private readonly int[] _gcStart = new int[3];
    private TimeSpan _pauseStart;

    private TraceSupport(bool enabled)
    {
        _enabled = enabled;
        ResetGcBaseline();
    }

    /// <summary>Restarts the GC delta baseline (call once inputs are loaded and the measured run begins).</summary>
    public void ResetGcBaseline()
    {
        for (var g = 0; g < 3; g++)
            _gcStart[g] = GC.CollectionCount(g);
        _pauseStart = GC.GetTotalPauseDuration();
    }

    /// <summary>Removes <c>--handshake</c> from <paramref name="args"/>; returns the support object (counters start now).</summary>
    public static TraceSupport From(ref string[] args)
    {
        var enabled = args.Contains("--handshake");
        if (enabled)
            args = args.Where(a => a != "--handshake").ToArray();
        return new TraceSupport(enabled);
    }

    /// <summary>When enabled: announce <paramref name="point"/> on stdout and block until the parent writes a line.</summary>
    public void Wait(string point)
    {
        if (!_enabled)
            return;
        Console.WriteLine(Marker + point);
        Console.Out.Flush();
        Console.In.ReadLine();
    }

    /// <summary>Prints the GC deltas since construction as one machine-readable line.</summary>
    public void PrintGcStats()
    {
        var pause = (GC.GetTotalPauseDuration() - _pauseStart).TotalMilliseconds;
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"GCSTATS gen0={GC.CollectionCount(0) - _gcStart[0]} gen1={GC.CollectionCount(1) - _gcStart[1]} gen2={GC.CollectionCount(2) - _gcStart[2]} pause_ms={pause:F3}"));
    }
}
