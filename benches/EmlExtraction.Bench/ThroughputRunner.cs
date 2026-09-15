using System.Diagnostics;
using System.Runtime.InteropServices;
using EmlExtraction;

namespace EmlExtraction.Bench;

internal static class ThroughputRunner
{
    public static void Run()
    {
        var detector = new EmlDetector();
        var extractor = new EmlExtractor();
        var corpus = Corpus.Create();

        Console.WriteLine("EML extraction throughput");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Arch: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"CPU: {CpuName()}");
        Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Processors: {Environment.ProcessorCount}");
        Console.WriteLine();
        Console.WriteLine($"{"case",-18} {"bytes",10} {"detect/s",12} {"extract/s",12} {"alloc MB/s",12}");

        foreach (var sample in corpus)
            PrintRow(detector, extractor, sample);
    }

    private static void PrintRow(EmlDetector detector, EmlExtractor extractor, Sample sample)
    {
        for (var i = 0; i < 50; i++)
        {
            detector.IsEml(sample.Bytes);
            extractor.Extract(sample.Bytes);
        }

        var detectN = 0;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 800)
        {
            detector.IsEml(sample.Bytes);
            detectN++;
        }

        var detectPerSec = detectN / sw.Elapsed.TotalSeconds;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var extractN = 0;
        sw.Restart();
        while (sw.ElapsedMilliseconds < 1200)
        {
            extractor.Extract(sample.Bytes);
            extractN++;
        }

        var after = GC.GetTotalAllocatedBytes(precise: true);
        var extractPerSec = extractN / sw.Elapsed.TotalSeconds;
        var allocMbPerSec = (after - before) / sw.Elapsed.TotalSeconds / (1024 * 1024);
        Console.WriteLine($"{sample.Name,-18} {sample.Bytes.Length,10} {detectPerSec,12:F0} {extractPerSec,12:F0} {allocMbPerSec,12:F1}");
    }

    private static string CpuName()
    {
        if (!OperatingSystem.IsMacOS())
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
        try
        {
            var psi = new ProcessStartInfo("sysctl", "-n machdep.cpu.brand_string") { RedirectStandardOutput = true };
            using var process = Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Trim() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
