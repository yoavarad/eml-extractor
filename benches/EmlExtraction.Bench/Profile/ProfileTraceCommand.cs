using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using EmlExtraction.Bench.Sweep;

namespace EmlExtraction.Bench.Profile;

/// <summary>
/// <c>profile-trace [--corpus &lt;path&gt;] [--out &lt;dir&gt;] [--tfms net8.0,net10.0] [--window-ms N] [--warmup-ms N]
/// [--min-passes N] [--max-input-bytes N]</c>: corpus-level run of the ADR-015 sweep harness (and the large-file pass) under
/// dotnet-trace (sampled thread time + gc-verbose) and dotnet-counters (System.Runtime CSV), plus a dotnet-gcdump at the
/// large-file peak, per TFM. Each run is a child process released by a stdin handshake once the tools are attached.
/// Output: artifacts/perf/&lt;stamp&gt;/ with manifest.json. Tools come from the local tool manifest (.config/dotnet-tools.json).
/// </summary>
internal static class ProfileTraceCommand
{
    private const int ExitUsage = 2;
    private const int ExitHardware = 3;
    private const int ExitRun = 7;
    private const string TraceProfiles = "dotnet-sampled-thread-time,gc-verbose";
    private const string Usage =
        "Usage: profile-trace [--corpus <path>] [--out <dir>] [--tfms net8.0,net10.0] [--window-ms N] [--warmup-ms N] [--min-passes N] [--max-input-bytes N]";

    private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ToolExitTimeout = TimeSpan.FromSeconds(120);

    private sealed record GcStats(int Gen0, int Gen1, int Gen2, double PauseMs);

    private sealed record RunRecord(
        string Name, string Tfm, string GcMode, int? Dop, string Set, int ExitCode, GcStats? Gc, List<string> Files);

    private sealed record Manifest(
        int SchemaVersion, string Stamp, HardwareBlock Hardware, string Corpus, bool CorpusPresent,
        string TraceProfiles, Dictionary<string, string> Tools, List<RunRecord> Runs);

    public static int Run(string[] args)
    {
        var repoRoot = RepoRoot.Find();
        var corpus = Path.Combine(repoRoot, "corpus", "internal");
        string? outDir = null;
        var tfms = new[] { "net8.0", "net10.0" };
        var passThrough = new List<string>(); // sweep timing options
        string? maxInput = null;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
                return Fail(ExitUsage, $"Missing value for {args[i]}. {Usage}");
            var v = args[i + 1];
            switch (args[i])
            {
                case "--corpus": corpus = Path.GetFullPath(v); break;
                case "--out": outDir = Path.GetFullPath(v); break;
                case "--tfms": tfms = v.Split(',', StringSplitOptions.RemoveEmptyEntries); break;
                case "--window-ms" or "--warmup-ms" or "--min-passes": passThrough.AddRange([args[i], v]); break;
                case "--max-input-bytes": maxInput = v; break;
                default: return Fail(ExitUsage, $"Unknown option {args[i]}. {Usage}");
            }
        }

        HardwareBlock hardware;
        try
        {
            hardware = HardwareProbe.Probe(repoRoot, simulateCpuUnreadable: false);
        }
        catch (HardwareProbeException ex)
        {
            return Fail(ExitHardware, ex.Message);
        }

        var haveCorpus = Directory.Exists(corpus);
        if (!haveCorpus)
            Console.WriteLine($"SKIPPED: public corpus not found at {corpus}; profiling the synthetic set only");
        var set = haveCorpus ? "all" : "synthetic";

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var artifacts = Path.GetFullPath(outDir ?? Path.Combine(repoRoot, "artifacts", "perf", stamp));
        Directory.CreateDirectory(artifacts);

        if (Tool(repoRoot, ["restore"], artifacts, "tool-restore") != 0)
            return Fail(ExitRun, "dotnet tool restore failed (see tool-restore.log).");

        var cores = Environment.ProcessorCount;
        var matrix = new List<(string Name, string Gc, int Dop)> { ("ws-dop1", "workstation", 1) };
        if (cores > 1)
            matrix.Add(($"ws-dop{cores}", "workstation", cores));
        matrix.Add(($"server-dop{cores}", "server", cores));

        var runs = new List<RunRecord>();
        foreach (var tfm in tfms)
        {
            var dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", tfm, "EmlExtraction.Bench.dll"));
            if (!File.Exists(dll))
                return Fail(ExitRun, $"{dll} not found. Build the bench for every TFM first: dotnet build benches/EmlExtraction.Bench -c Release");
            var tfmDir = Path.Combine(artifacts, tfm);
            Directory.CreateDirectory(tfmDir);

            foreach (var (name, gc, dop) in matrix)
            {
                var result = Path.Combine(tfmDir, $"{name}.sweep.json");
                string[] childArgs =
                [
                    dll, "sweep-child", "--handshake", "--set", set, "--corpus", corpus, "--dop", dop.ToString(CultureInfo.InvariantCulture),
                    "--gc-mode", gc, "--result-file", result, .. passThrough
                ];
                runs.Add(Traced(repoRoot, artifacts, tfm, name, gc, dop, set, childArgs, gcdump: false));
            }

            string[] largeArgs = [dll, "large", "--handshake", "--out", tfmDir, .. (maxInput is null ? [] : new[] { "--max-input-bytes", maxInput })];
            runs.Add(Traced(repoRoot, artifacts, tfm, "large", "workstation", null, "large-file", largeArgs, gcdump: true));
        }

        var manifest = new Manifest(1, stamp, hardware, corpus, haveCorpus, TraceProfiles, ToolVersions(repoRoot), runs);
        var manifestPath = Path.Combine(artifacts, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, SweepReport.Json));

        var missing = runs.SelectMany(r => r.Files).Where(f => !File.Exists(Path.Combine(artifacts, f))).ToList();
        var failed = runs.Where(r => r.ExitCode != 0 || r.Gc is null).Select(r => $"{r.Tfm}/{r.Name} (exit {r.ExitCode})").ToList();
        Console.WriteLine($"Manifest: {manifestPath}");
        if (missing.Count + failed.Count > 0)
            return Fail(ExitRun, $"failed runs: [{string.Join(", ", failed)}], missing files: [{string.Join(", ", missing)}]");
        return 0;
    }

    private static RunRecord Traced(string repoRoot, string artifacts, string tfm, string name, string gc, int? dop, string set, string[] childArgs, bool gcdump)
    {
        var rel = $"{tfm}/{name}";
        var files = new List<string> { rel + ".nettrace", rel + ".counters.csv" };
        if (gcdump)
            files.Add(rel + ".gcdump");
        Console.WriteLine();
        Console.WriteLine($"=== {tfm} {name} ({set}) ===");

        var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true };
        foreach (var a in childArgs)
            psi.ArgumentList.Add(a);
        psi.Environment["DOTNET_gcServer"] = gc == "server" ? "1" : "0";
        using var child = Process.Start(psi) ?? throw new InvalidOperationException("Could not start child process.");
        var pid = child.Id.ToString(CultureInfo.InvariantCulture);

        var tools = new List<Process>();
        GcStats? stats = null;
        string? line;
        while ((line = child.StandardOutput.ReadLine()) is not null)
        {
            Console.WriteLine(line);
            if (line == "EML-HANDSHAKE start")
            {
                var trace = Path.Combine(artifacts, rel + ".nettrace");
                var counters = Path.Combine(artifacts, rel + ".counters.csv");
                tools.Add(StartTool(repoRoot, ["collect", "-p", pid, "--profile", TraceProfiles, "-o", trace], artifacts, rel + ".trace", "dotnet-trace"));
                tools.Add(StartTool(repoRoot, ["collect", "-p", pid, "--counters", "System.Runtime", "--format", "csv", "--refresh-interval", "1", "-o", counters], artifacts, rel + ".counters", "dotnet-counters"));
                WaitForFiles(tools, trace, counters);
                child.StandardInput.WriteLine();
            }
            else if (line == "EML-HANDSHAKE peak")
            {
                var code = Tool(repoRoot, ["collect", "-p", pid, "--timeout", "300", "-o", Path.Combine(artifacts, rel + ".gcdump")], artifacts, rel + ".gcdump", "dotnet-gcdump");
                if (code != 0)
                    Console.Error.WriteLine($"dotnet-gcdump exited {code} (see {rel}.gcdump.log)");
                child.StandardInput.WriteLine();
            }
            else if (line.StartsWith("GCSTATS ", StringComparison.Ordinal))
            {
                stats = ParseGcStats(line);
            }
        }

        child.WaitForExit();
        foreach (var t in tools)
        {
            if (!t.WaitForExit(ToolExitTimeout))
                t.Kill(entireProcessTree: true);
            t.Dispose();
        }

        return new RunRecord(name, tfm, gc, dop, set, child.ExitCode, stats, files);
    }

    private static void WaitForFiles(List<Process> tools, params string[] paths)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < AttachTimeout && !tools.Any(t => t.HasExited) && !paths.All(p => File.Exists(p) && new FileInfo(p).Length > 0))
            Thread.Sleep(200);
    }

    private static GcStats ParseGcStats(string line)
    {
        var kv = line["GCSTATS ".Length..].Split(' ').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        return new GcStats(int.Parse(kv["gen0"], CultureInfo.InvariantCulture), int.Parse(kv["gen1"], CultureInfo.InvariantCulture),
            int.Parse(kv["gen2"], CultureInfo.InvariantCulture), double.Parse(kv["pause_ms"], CultureInfo.InvariantCulture));
    }

    /// <summary>Runs <c>dotnet tool run &lt;tool&gt; ...</c> (or <c>dotnet tool restore</c> when no tool) to completion; output goes to <c>&lt;log&gt;.log</c>.</summary>
    private static int Tool(string repoRoot, string[] args, string artifacts, string log, string? tool = null)
    {
        using var p = StartTool(repoRoot, args, artifacts, log, tool);
        p.WaitForExit();
        return p.ExitCode;
    }

    private static Process StartTool(string repoRoot, string[] args, string artifacts, string log, string? tool)
    {
        var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = repoRoot, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        psi.ArgumentList.Add("tool");
        if (tool is null)
        {
            psi.ArgumentList.Add(args[0]);
        }
        else
        {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(tool);
            foreach (var a in args)
                psi.ArgumentList.Add(a);
        }

        var logPath = Path.Combine(artifacts, log + ".log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var writer = new StreamWriter(logPath) { AutoFlush = true };
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { lock (writer) writer.WriteLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { lock (writer) writer.WriteLine(e.Data); };
        p.Exited += (_, _) =>
        {
            p.WaitForExit(); // drain async output to EOF before closing the log
            lock (writer)
                writer.Dispose();
        };
        p.Start();
        p.StandardInput.Close(); // tools must not read the console
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static Dictionary<string, string> ToolVersions(string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, ".config", "dotnet-tools.json")));
        return doc.RootElement.GetProperty("tools").EnumerateObject()
            .ToDictionary(t => t.Name, t => t.Value.GetProperty("version").GetString() ?? "");
    }

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine($"profile-trace: {message}");
        return code;
    }
}
