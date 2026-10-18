using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Text;
using System.Text.Json;
using EmlExtraction.Bench.Profile;

namespace EmlExtraction.Bench.Sweep;

/// <summary>
/// <c>large</c>: single-thread large-file pass (ADR-015). Messages at 25/50/100% of the limit, each with one base64
/// attachment, plus one at limit+1 that must yield <c>input_too_large</c>. Prints M, peak working set and the sizing formula.
/// </summary>
internal static class LargeFileCommand
{
    private const string Usage =
        "Usage: large [--max-input-bytes N] [--sample-ms N] [--sweep <sweep.json>] [--out <dir>] [--handshake]";

    private const string Rerun = """
        ## How the team reruns this

        1. On the target server class and GC mode: `dotnet run -c Release --project benches/EmlExtraction.Bench -- sweep` (writes `sweep-results/sweep-<stamp>.json`).
        2. `dotnet run -c Release --project benches/EmlExtraction.Bench -- large --max-input-bytes <configured limit>` (reads the newest sweep JSON, or pass `--sweep <json>`).
        3. Plug the target rate R into the formula above. Use the same machine for both steps; the large-file pass needs free RAM of about 5x the limit.
        """;

    private sealed record Pass(string Label, long InputBytes, long PreWorkingSet, long PeakWorkingSet, double Seconds, bool Ok, string Note)
    {
        public double M => LargeFileMath.PeakGrowthPerInputByte(PeakWorkingSet, PreWorkingSet, InputBytes);
    }

    public static int Run(string[] args)
    {
        var trace = TraceSupport.From(ref args);
        var repoRoot = RepoRoot.Find();
        long max = EmlExtractor.DefaultMaxInputBytes;
        var sampleMs = 50;
        string? sweepPath = null;
        var outDir = Path.Combine(repoRoot, "sweep-results");
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
                return Fail($"Missing value for {args[i]}. {Usage}");
            var v = args[i + 1];
            switch (args[i])
            {
                case "--max-input-bytes" when long.TryParse(v, out var m) && m >= LargeFileMath.MinMessageBytes * 4L && m < int.MaxValue: max = m; break;
                case "--sample-ms" when int.TryParse(v, out var s) && s >= 1: sampleMs = s; break;
                case "--sweep": sweepPath = Path.GetFullPath(v); break;
                case "--out": outDir = Path.GetFullPath(v); break;
                default: return Fail($"Unknown or invalid option {args[i]} {v}. {Usage}");
            }
        }

        if (sweepPath is not null && !File.Exists(sweepPath))
            return Fail($"Sweep file not found: {sweepPath}");

        var extractor = new EmlExtractor(max);
        extractor.Extract(LargeFileMath.BuildMessage(Math.Min(65_536, max))); // JIT warm-up so it does not inflate the first M
        var passes = new List<Pass>();
        trace.Wait("start");
        trace.ResetGcBaseline();
        foreach (var percent in new[] { 25, 50, 100 })
            passes.Add(Measure($"{percent}%", max * percent / 100, extractor, sampleMs, expectAttachment: true, percent == 100 ? trace : null));
        passes.Add(Measure("limit+1", max + 1, extractor, sampleMs, expectAttachment: false, null));
        trace.PrintGcStats();

        var sb = new StringBuilder();
        sb.AppendLine("# Large-file pass");
        sb.AppendLine();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Single thread, {(GCSettings.IsServerGC ? "Server" : "Workstation")} GC, maxInputBytes {max}, working set sampled every {sampleMs} ms, forced full GC before each input. M = (peak - pre-run WS) / input bytes."));
        sb.AppendLine();
        sb.AppendLine("| Input | Bytes | Pre WS MB | Peak WS MB | M | Seconds | Result |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var p in passes)
        {
            var m = p.Label == "limit+1" ? "n/a" : p.M.ToString("F2", CultureInfo.InvariantCulture);
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {p.Label} | {p.InputBytes} | {Mb(p.PreWorkingSet):F1} | {Mb(p.PeakWorkingSet):F1} | {m} | {p.Seconds:F2} | {(p.Ok ? "ok" : "FAIL")}: {p.Note} |"));
        }

        var okPasses = passes.Where(p => p.Label != "limit+1" && p.Ok).ToList();
        // M is the worst (largest) observed across the in-limit inputs: sizing must cover the worst case.
        double? mWorst = okPasses.Count == 0 ? null : okPasses.Max(p => p.M);
        sb.AppendLine();
        sb.AppendLine(Formula(mWorst, max, sweepPath, repoRoot));
        sb.AppendLine(Rerun);

        Console.Write(sb);
        Directory.CreateDirectory(outDir);
        var file = Path.Combine(outDir, $"large-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(file, sb.ToString());
        Console.WriteLine($"Markdown: {file}");
        return okPasses.Count == 3 && passes[^1].Ok ? 0 : 1;
    }

    private static Pass Measure(string label, long size, EmlExtractor extractor, int sampleMs, bool expectAttachment, TraceSupport? peakHook)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var self = Process.GetCurrentProcess();
        self.Refresh();
        var pre = self.WorkingSet64;
        var input = LargeFileMath.BuildMessage(size); // built after `pre`, so M includes the resident input
        self.Refresh();
        long peak = self.WorkingSet64;
        var stop = false;
        var sampler = new Thread(() =>
        {
            using var p = Process.GetCurrentProcess();
            while (!Volatile.Read(ref stop))
            {
                p.Refresh();
                var ws = p.WorkingSet64;
                if (ws > Interlocked.Read(ref peak))
                    Interlocked.Exchange(ref peak, ws);
                Thread.Sleep(sampleMs);
            }
        })
        { IsBackground = true };
        sampler.Start();
        var sw = Stopwatch.StartNew();
        var result = extractor.Extract(input);
        sw.Stop();
        Volatile.Write(ref stop, true);
        sampler.Join();
        self.Refresh();
        peak = Math.Max(Interlocked.Read(ref peak), self.WorkingSet64);
        peakHook?.Wait("peak"); // input and result still live: the parent takes the gcdump here

        bool ok;
        string note;
        var detail = $"attachments={result.Attachments.Count}, warnings={string.Join(";", result.Warnings)}";
        if (expectAttachment)
        {
            ok = result.Attachments.Count == 1 && result.Warnings.Count == 0;
            note = ok ? "1 attachment" : detail;
        }
        else
        {
            ok = result.Warnings.Count == 1 && result.Warnings[0].StartsWith("input_too_large", StringComparison.Ordinal)
                && result.Attachments.Count == 0;
            note = ok ? "input_too_large, no parse" : "unexpected: " + detail;
        }

        GC.KeepAlive(input);
        return new Pass(label, input.Length, pre, peak, sw.Elapsed.TotalSeconds, ok, note);
    }

    private static string Formula(double? m, long max, string? sweepPath, string repoRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Sizing formula");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("cores_needed   = ceil(R / (T(1) x E(n)))      R = target files/s, n = cores per server");
        sb.AppendLine("servers_needed = ceil(R / T(n_best))");
        sb.AppendLine("ram_per_server = P(n_best) + n_best x max_input_bytes x M");
        sb.AppendLine("```");
        sb.AppendLine();
        var mText = m is null ? "n/a (large-file pass failed)" : m.Value.ToString("F2", CultureInfo.InvariantCulture);
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Measured: max_input_bytes = {max}, M = {mText} (largest of the 25/50/100% passes)."));

        var dir = Path.Combine(repoRoot, "sweep-results");
        sweepPath ??= Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "sweep-*.json").Order(StringComparer.Ordinal).LastOrDefault()
            : null;
        SweepReport? report = null;
        try
        {
            if (sweepPath is not null && File.Exists(sweepPath))
                report = JsonSerializer.Deserialize<SweepReport>(File.ReadAllText(sweepPath), SweepReport.Json);
        }
        catch (JsonException)
        {
            // Unreadable sweep file: reported below as "no measured sweep".
        }

        var set = report?.Runs.FirstOrDefault(r => r.GcMode == "workstation")?.Sets
            .Where(s => s.Status == "measured" && s.Extract.Count > 0)
            .OrderByDescending(s => s.Name == "mix").ThenByDescending(s => s.Name == "public").FirstOrDefault();
        if (set is null)
        {
            sb.AppendLine("T(1), E(n), n_best, P(n_best): n/a, no measured sweep found. Run `sweep`, then rerun `large` (or pass `--sweep <json>`).");
            return sb.ToString();
        }

        var n = LargeFileMath.NBest(set.Extract.Select(p => p.FilesPerSec).ToList());
        var best = set.Extract[n - 1];
        var t1 = set.Extract[0].FilesPerSec;
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"From `{Path.GetFileName(sweepPath)}` (set {set.Name}, Workstation GC, extract): T(1) = {t1:F1} files/s, n_best = {n}, T(n_best) = {best.FilesPerSec:F1} files/s, E(n_best) = {best.Efficiency:F3}, P(n_best) = {Mb(best.PeakWorkingSetBytes):F1} MB."));
        if (m is not null)
        {
            var ram = LargeFileMath.RamPerServer(best.PeakWorkingSetBytes, n, max, m.Value);
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"ram_per_server = {Mb(best.PeakWorkingSetBytes):F1} MB + {n} x {Mb(max):F1} MB x {m.Value:F2} = {Mb(ram):F1} MB."));
        }

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Per 1 file/s of target: cores_needed = ceil(1 / ({t1:F1} x {best.Efficiency:F3})), servers_needed = ceil(1 / {best.FilesPerSec:F1}); multiply R in."));
        return sb.ToString();
    }

    private static double Mb(double bytes) => bytes / (1024.0 * 1024);

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"ERROR: {message}");
        return 2;
    }
}
