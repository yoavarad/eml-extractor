using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using EmlExtraction.Bench.Sweep;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace EmlExtraction.Bench.Profile;

/// <summary>
/// <c>profile [--corpus &lt;path&gt;] [--job default|short|dry] [--out &lt;dir&gt;] [--simulate-cpu-unreadable]</c>:
/// runs the per-stage benchmarks (ADR-020) on net8.0 and net10.0 with Memory/Threading diagnosers and
/// EventPipe CPU sampling + GC verbose traces. BDN results and traces go under artifacts/perf/&lt;stamp&gt;/.
/// </summary>
internal static class ProfileCommand
{
    private const int ExitUsage = 2;
    private const int ExitHardware = 3;
    private const int ExitBenchmarks = 6;

    private static readonly Type[] Stages =
        [typeof(RequestStage), typeof(DetectStage), typeof(ParseStage), typeof(MetadataStage), typeof(BodyStage), typeof(AttachmentsStage)];

    public static int Run(string[] args)
    {
        var repoRoot = RepoRoot.Find();
        string? corpus = null, outDir = null;
        var job = "default";
        var simulateCpuUnreadable = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--corpus" when i + 1 < args.Length: corpus = args[++i]; break;
                case "--job" when i + 1 < args.Length: job = args[++i]; break;
                case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
                case "--simulate-cpu-unreadable": simulateCpuUnreadable = true; break;
                default: return Fail(ExitUsage, $"Unknown or incomplete option '{args[i]}'.");
            }
        }

        var baseJob = job switch
        {
            "default" => Job.Default,
            "short" => Job.ShortRun,
            "dry" => Job.Dry,
            _ => null
        };
        if (baseJob is null)
            return Fail(ExitUsage, $"--job must be default, short or dry (got '{job}').");

        HardwareBlock hardware;
        try
        {
            hardware = HardwareProbe.Probe(repoRoot, simulateCpuUnreadable);
        }
        catch (HardwareProbeException ex)
        {
            return Fail(ExitHardware, ex.Message);
        }

        Console.WriteLine("EML extraction per-stage profile (ADR-020)");
        var hardwareJson = JsonSerializer.Serialize(hardware, SweepReport.Json);
        Console.WriteLine(hardwareJson);

        corpus = Path.GetFullPath(corpus ?? Path.Combine(repoRoot, "corpus", "internal"));
        var haveCorpus = Directory.Exists(corpus);
        Environment.SetEnvironmentVariable(ProfileSamples.CorpusEnvVar, haveCorpus ? corpus : null);
        if (!haveCorpus)
            Console.WriteLine($"SKIPPED: public corpus not found at {corpus}");

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var artifacts = Path.GetFullPath(outDir ?? Path.Combine(repoRoot, "artifacts", "perf", stamp));
        Directory.CreateDirectory(artifacts);
        File.WriteAllText(Path.Combine(artifacts, "hardware.json"), hardwareJson);

        var summaries = BenchmarkRunner.Run(Stages, CreateConfig(baseJob, haveCorpus ? corpus : null, artifacts));
        Console.WriteLine($"Artifacts: {artifacts}");
        return summaries.Length == 0 || summaries.Any(s => s.HasCriticalValidationErrors || s.Reports.Any(r => !r.Success)) ? ExitBenchmarks : 0;
    }

    private static ManualConfig CreateConfig(Job baseJob, string? corpus, string artifacts)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(artifacts)
            .WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(80))
            .AddDiagnoser(MemoryDiagnoser.Default, ThreadingDiagnoser.Default, CreateEventPipeProfiler())
            .AddColumn(new CopyFactorColumn())
            .AddExporter(JsonExporter.Full);
        foreach (var runtime in new[] { CoreRuntime.Core80, CoreRuntime.Core10_0 })
        {
            var job = baseJob.WithRuntime(runtime).WithId(runtime.MsBuildMoniker);
            if (corpus is not null)
                job = job.WithEnvironmentVariable(ProfileSamples.CorpusEnvVar, corpus);
            config.AddJob(job);
        }

        return config;
    }

    /// <summary>CPU sampling (BDN's CpuSampling profile) plus the runtime provider at verbose GC level (GC events, allocation ticks incl. LOH).</summary>
    private static EventPipeProfiler CreateEventPipeProfiler()
    {
        const long keywords = (long)(ClrTraceEventParser.Keywords.Default
                                     | ClrTraceEventParser.Keywords.GC
                                     | ClrTraceEventParser.Keywords.GCHeapAndTypeNames
                                     | ClrTraceEventParser.Keywords.Type);
        return new EventPipeProfiler(
            EventPipeProfile.CpuSampling,
            [new EventPipeProvider(ClrTraceEventParser.ProviderName, EventLevel.Verbose, keywords)]);
    }

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine($"profile: {message}");
        return code;
    }
}
