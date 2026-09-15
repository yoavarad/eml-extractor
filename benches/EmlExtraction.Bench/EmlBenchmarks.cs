using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using EmlExtraction;

namespace EmlExtraction.Bench;

[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class EmlBenchmarks
{
    private readonly EmlDetector _detector = new();
    private readonly EmlExtractor _extractor = new();
    private byte[] _tiny = [];
    private byte[] _attach = [];

    [GlobalSetup]
    public void Setup()
    {
        _tiny = Corpus.ToBytes(Corpus.Plain("tiny", "hello"));
        _attach = Corpus.Create().First(s => s.Name == "attach-1mb").Bytes;
    }

    [Benchmark]
    public bool DetectTiny() => _detector.IsEml(_tiny);

    [Benchmark]
    public EmlExtractionResult ExtractTiny() => _extractor.Extract(_tiny);

    [Benchmark]
    public EmlExtractionResult Extract1MbAttach() => _extractor.Extract(_attach);
}
