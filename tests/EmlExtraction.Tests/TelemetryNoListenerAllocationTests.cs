using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace EmlExtraction.Tests;

/// <summary>ydk:nfr:observability/metrics-overhead: with no MeterListener and no ActivityListener the telemetry hooks allocate nothing (ADR-021).</summary>
[Collection(TelemetryCollectionDefinition.Name)]
public sealed class TelemetryNoListenerAllocationTests
{
    private const int Iterations = 1000;

    private static readonly byte[] Tiny = "From: a@example.com\r\nTo: b@example.com\r\nSubject: s\r\nDate: Tue, 15 Sep 2026 07:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain\r\n\r\nx\r\n"u8.ToArray();

    [SkippableFact]
    public void TelemetryHooksAllocateZeroBytesWithoutListener()
    {
        // net8.0 BCL quirk: once any MeterListener has been attached, Instrument.Enabled stays true after the listener is
        // disposed (fixed in later runtimes), so earlier tests in this process can make the no-listener state unreachable.
        Skip.If(AnyInstrumentEnabled(), "an instrument is still Enabled in this process after an earlier listener (net8.0 BCL quirk)");

        var result = new EmlExtractor().Extract(Tiny);

        for (var i = 0; i < 5; i++)
            Run(result); // warm up JIT and lazy statics
        Assert.Equal(0, Run(result));
    }

    private static long Run(EmlExtractionResult result)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            var start = Stopwatch.GetTimestamp();
            EmlExtractionTelemetry.RecordInputSize(Tiny.Length);
            var activity = EmlExtractionTelemetry.StartExtract(Tiny.Length);
            using (new EmlExtractionTelemetry.ExtractScope(activity))
            {
                EmlExtractionTelemetry.StartStage(activity, "eml_extraction.parse")?.Dispose();
                var outcome = EmlExtractionTelemetry.RecordExtract(start, null, result);
                EmlExtractionTelemetry.TagExtract(activity, outcome, result);
            }

            EmlExtractionTelemetry.RecordDetect(true);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static bool AnyInstrumentEnabled()
        => typeof(EmlExtractionTelemetry)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Select(f => f.GetValue(null))
            .OfType<Instrument>()
            .Any(i => i.Enabled);
}
