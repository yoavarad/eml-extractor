using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Text.Json;

namespace EmlExtraction.Bench.Sweep;

/// <summary>
/// <c>sweep</c>: probes hardware, then runs the DOP sweep once per GC mode, each in a child process
/// (<c>sweep-child</c>) with <c>DOTNET_gcServer</c> set explicitly. Writes JSON + markdown.
/// </summary>
internal static class SweepCommand
{
    private const int ExitUsage = 2;
    private const int ExitHardware = 3;
    private const int ExitGcMismatch = 4;
    private const int ExitInputs = 5;

    private static readonly string[] Sets = ["synthetic", "public", "mix", "all"];
    private static readonly string[] GcModes = ["workstation", "server"];

    public static int Run(string[] args)
    {
        if (!TryParse(args, out var o, out var error))
            return Fail(ExitUsage, error);

        var hardware = (HardwareBlock?)null;
        try
        {
            hardware = HardwareProbe.Probe(o.RepoRoot, o.SimulateCpuUnreadable);
        }
        catch (HardwareProbeException ex)
        {
            return Fail(ExitHardware, ex.Message);
        }

        Console.WriteLine("EML extraction DOP sweep (ADR-015)");
        foreach (var line in JsonSerializer.Serialize(hardware, SweepReport.Json).Split('\n'))
            Console.WriteLine(line.TrimEnd());

        var runs = new List<GcRun>();
        foreach (var mode in GcModes)
        {
            var resultFile = Path.Combine(Path.GetTempPath(), $"eml-sweep-{Environment.ProcessId}-{mode}.json");
            var exit = RunChild(mode, resultFile, args);
            if (exit != 0)
                return Fail(exit, $"{mode} GC child exited with {exit}.");
            runs.Add(JsonSerializer.Deserialize<GcRun>(File.ReadAllText(resultFile), SweepReport.Json)!);
            File.Delete(resultFile);
        }

        var report = new SweepReport(SweepReport.CurrentSchemaVersion, hardware, o.Parameters, runs);
        Directory.CreateDirectory(o.OutDir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var json = Path.Combine(o.OutDir, $"sweep-{stamp}.json");
        var md = Path.Combine(o.OutDir, $"sweep-{stamp}.md");
        File.WriteAllText(json, JsonSerializer.Serialize(report, SweepReport.Json));
        File.WriteAllText(md, report.ToMarkdown());
        Console.WriteLine();
        Console.WriteLine($"JSON: {json}");
        Console.WriteLine($"Markdown: {md}");
        return 0;
    }

    /// <summary><c>sweep-child --gc-mode &lt;mode&gt; --result-file &lt;path&gt; [sweep options]</c>.</summary>
    public static int Child(string[] args)
    {
        if (!TryParse(args, out var o, out var error))
            return Fail(ExitUsage, error);
        if (o.GcMode is null || o.ResultFile is null)
            return Fail(ExitUsage, "sweep-child needs --gc-mode and --result-file.");
        var wantServer = o.GcMode == "server";
        if (GCSettings.IsServerGC != wantServer)
            return Fail(ExitGcMismatch, $"Requested {o.GcMode} GC but the runtime reports IsServerGC={GCSettings.IsServerGC}.");

        Console.WriteLine();
        Console.WriteLine($"=== GC mode: {o.GcMode} (DOTNET_gcServer={Environment.GetEnvironmentVariable("DOTNET_gcServer")}) ===");

        byte[][] synthetic;
        try
        {
            synthetic = o.Set is "synthetic" or "mix" or "all" ? SweepInputs.Synthetic(o.RepoRoot) : [];
        }
        catch (DirectoryNotFoundException ex)
        {
            return Fail(ExitInputs, ex.Message);
        }

        var wantPublic = o.Set is "public" or "mix" or "all";
        var publicSet = wantPublic ? SweepInputs.Public(o.Parameters.Corpus) : null;
        var publicSkip = $"public corpus not found at {o.Parameters.Corpus}";
        if (!wantPublic)
            Console.WriteLine($"public: not requested (--set {o.Set})");

        var extractor = new EmlExtractor();
        var detector = new EmlDetector();
        var results = new List<SetResult>();
        if (o.Set is "synthetic" or "all")
            results.Add(Measure("synthetic", synthetic, o.Parameters, extractor, detector));
        if (wantPublic && o.Set != "mix")
            results.Add(publicSet is null ? Skipped("public", publicSkip) : Measure("public", publicSet, o.Parameters, extractor, detector));
        if (o.Set is "mix" or "all")
        {
            results.Add(publicSet is null
                ? Skipped("mix", publicSkip)
                : Measure("mix", SweepInputs.Mix(synthetic, publicSet), o.Parameters, extractor, detector));
        }

        File.WriteAllText(o.ResultFile, JsonSerializer.Serialize(new GcRun(o.GcMode, results), SweepReport.Json));
        return 0;
    }

    private static SetResult Skipped(string name, string reason)
    {
        Console.WriteLine($"SKIPPED: {reason}");
        Console.WriteLine($"set '{name}': skipped");
        return new SetResult(name, "skipped", reason, 0, 0, [], []);
    }

    private static SetResult Measure(string name, byte[][] inputs, SweepParameters p, EmlExtractor extractor, EmlDetector detector)
    {
        var inputBytes = inputs.Sum(i => (long)i.Length);
        Console.WriteLine();
        Console.WriteLine($"--- set {name}: {inputs.Length} messages, {inputBytes} bytes ---");
        var extract = Sweep("extract", inputs, p, b => extractor.Extract(b));
        var detect = Sweep("detect", inputs, p, b => detector.IsEml(b));
        return new SetResult(name, "measured", null, inputs.Length, inputBytes, extract, detect);
    }

    private static List<SweepPoint> Sweep(string op, byte[][] inputs, SweepParameters p, Action<byte[]> action)
    {
        Console.WriteLine($"{op}:");
        Console.WriteLine(SweepReport.Header());
        var points = new List<SweepPoint>(p.MaxDop);
        for (var n = 1; n <= p.MaxDop; n++)
        {
            var point = SweepRunner.Run(inputs, n, action, p);
            var t1 = n == 1 ? point.FilesPerSec : points[0].FilesPerSec;
            point = point with { Efficiency = SweepMath.Efficiency(point.FilesPerSec, n, t1) };
            points.Add(point);
            Console.WriteLine(SweepReport.Row(point));
        }

        return points;
    }

    private static int RunChild(string mode, string resultFile, string[] parentArgs)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown.");
        var psi = new ProcessStartInfo(processPath) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add(typeof(SweepCommand).Assembly.Location);
        psi.ArgumentList.Add("sweep-child");
        foreach (var a in parentArgs)
            psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--gc-mode");
        psi.ArgumentList.Add(mode);
        psi.ArgumentList.Add("--result-file");
        psi.ArgumentList.Add(resultFile);
        psi.Environment["DOTNET_gcServer"] = mode == "server" ? "1" : "0";
        using var child = Process.Start(psi) ?? throw new InvalidOperationException("Could not start child process.");
        child.WaitForExit();
        return child.ExitCode;
    }

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine($"ERROR: {message}");
        return code;
    }

    private sealed record Options(
        SweepParameters Parameters,
        string Set,
        string RepoRoot,
        string OutDir,
        bool SimulateCpuUnreadable,
        string? GcMode,
        string? ResultFile);

    private static bool TryParse(string[] args, out Options options, out string error)
    {
        options = null!;
        error = "";
        var repoRoot = RepoRoot.Find();
        string set = "all", corpus = Path.Combine(repoRoot, "corpus", "internal"), outDir = Path.Combine(repoRoot, "sweep-results");
        int warmup = 2000, window = 10_000, passes = 3, sample = 50;
        bool simulate = false;
        string? gcMode = null, resultFile = null;
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (key == "--simulate-cpu-unreadable")
            {
                simulate = true;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                error = $"Missing value for {key}.";
                return false;
            }

            var value = args[++i];
            switch (key)
            {
                case "--set": set = value; break;
                case "--corpus": corpus = Path.GetFullPath(value); break;
                case "--out": outDir = Path.GetFullPath(value); break;
                case "--warmup-ms" when int.TryParse(value, out var v) && v >= 0: warmup = v; break;
                case "--window-ms" when int.TryParse(value, out var v) && v > 0: window = v; break;
                case "--min-passes" when int.TryParse(value, out var v) && v >= 1: passes = v; break;
                case "--sample-ms" when int.TryParse(value, out var v) && v >= 1: sample = v; break;
                case "--gc-mode" when GcModes.Contains(value): gcMode = value; break;
                case "--result-file": resultFile = value; break;
                default:
                    error = $"Unknown or invalid option {key} {value}. Usage: sweep [--set synthetic|public|mix|all] [--corpus <path>] " +
                        "[--warmup-ms N] [--window-ms N] [--min-passes N] [--sample-ms N] [--out <dir>] [--simulate-cpu-unreadable]";
                    return false;
            }
        }

        if (!Sets.Contains(set))
        {
            error = $"--set must be one of {string.Join(", ", Sets)}.";
            return false;
        }

        options = new Options(
            new SweepParameters(warmup, window, passes, sample, Environment.ProcessorCount, set, corpus),
            set, repoRoot, outDir, simulate, gcMode, resultFile);
        return true;
    }
}
