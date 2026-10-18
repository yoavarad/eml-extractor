using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace EmlExtraction.Bench;

public enum ListenerMode
{
    None,
    Meter,
    Activity
}

/// <summary>
/// Telemetry overhead (ADR-021, ydk:nfr:observability/metrics-overhead): Extract and Detect with no listener, a
/// MeterListener on every EmlExtraction instrument, and an ActivityListener sampling AllData. Synthetic inputs only.
/// Run: dotnet run -c Release --project benches/EmlExtraction.Bench -- overhead
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class OverheadBenchmarks
{
    private readonly EmlDetector _detector = new();
    private readonly EmlExtractor _extractor = new();
    private byte[] _tiny = [];
    private byte[] _small = [];
    private byte[] _attach = [];
    private MeterListener? _meterListener;
    private ActivityListener? _activityListener;

    [Params(ListenerMode.None, ListenerMode.Meter, ListenerMode.Activity)]
    public ListenerMode Mode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var samples = Corpus.Create();
        _tiny = samples.First(s => s.Name == "tiny").Bytes;
        _small = samples.First(s => s.Name == "small-20kb").Bytes;
        _attach = samples.First(s => s.Name == "attach-1mb").Bytes;

        if (Mode == ListenerMode.Meter)
        {
            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == EmlExtractionTelemetry.Name)
                        listener.EnableMeasurementEvents(instrument);
                }
            };
            _meterListener.SetMeasurementEventCallback<double>((_, _, _, _) => { });
            _meterListener.SetMeasurementEventCallback<long>((_, _, _, _) => { });
            _meterListener.SetMeasurementEventCallback<int>((_, _, _, _) => { });
            _meterListener.Start();
        }
        else if (Mode == ListenerMode.Activity)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == EmlExtractionTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(_activityListener);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _meterListener?.Dispose();
        _activityListener?.Dispose();
    }

    [Benchmark]
    public bool DetectTiny() => _detector.IsEml(_tiny);

    [Benchmark]
    public EmlExtractionResult ExtractTiny() => _extractor.Extract(_tiny);

    [Benchmark]
    public EmlExtractionResult ExtractSmall20Kb() => _extractor.Extract(_small);

    [Benchmark]
    public EmlExtractionResult ExtractAttach1Mb() => _extractor.Extract(_attach);
}
