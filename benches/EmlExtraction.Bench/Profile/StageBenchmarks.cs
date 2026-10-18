using BenchmarkDotNet.Attributes;
using MimeKit;

namespace EmlExtraction.Bench.Profile;

// One class per ADR-020 stage. Inputs are loaded and, for the post-parse stages, parsed in GlobalSetup,
// so each benchmark measures only its stage.

public abstract class StageBenchmark
{
    [ParamsSource(typeof(ProfileSamples), nameof(ProfileSamples.Names))]
    public string Sample { get; set; } = "";

    protected byte[] Bytes { get; private set; } = [];

    protected MimeMessage Message { get; private set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        Bytes = ProfileSamples.Load(Sample);
        Message = EmlExtractor.Parse(Bytes);
        AfterSetup();
    }

    protected virtual void AfterSetup()
    {
    }
}

/// <summary>request: the public wrapper and stage orchestration (ADR-012). Profiling-only stage.</summary>
public class RequestStage : StageBenchmark
{
    private readonly EmlExtractor _extractor = new();

    [Benchmark(Baseline = true)]
    public EmlExtractionResult ExtractBytes() => _extractor.Extract(Bytes);

    [Benchmark]
    public EmlExtractionResult ExtractRequest() => _extractor.Extract(new EmlExtractionRequest { FileBytes = Bytes, FileName = Sample });
}

/// <summary>detect: <see cref="EmlDetector"/>. Profiling-only stage.</summary>
public class DetectStage : StageBenchmark
{
    private readonly EmlDetector _detector = new();

    [Benchmark]
    public EmlDetectionResult Detect() => _detector.Detect(Bytes);

    [Benchmark]
    public bool IsEml() => _detector.IsEml(Bytes);
}

/// <summary>parse: MimeKit parse incl. mbox "From " and BOM handling (ADR-004 parse stage).</summary>
public class ParseStage : StageBenchmark
{
    [Benchmark]
    public MimeMessage Parse() => EmlExtractor.Parse(Bytes);
}

/// <summary>metadata: header mapping (ADR-004 metadata stage).</summary>
public class MetadataStage : StageBenchmark
{
    [Benchmark]
    public Dictionary<string, string> Map() => HeaderMapper.Map(Message, out _);
}

/// <summary>body: main-content selection and subject/body direction; mirrors the ADR-004 body stage in <see cref="EmlExtractor"/>.</summary>
public class BodyStage : StageBenchmark
{
    private string? _subject;

    protected override void AfterSetup() =>
        _subject = HeaderMapper.Map(Message, out _).GetValueOrDefault("subject");

    [Benchmark]
    public (string Subject, string Content) Body()
    {
        var body = BodySelector.Select(Message.Body);
        string? content = null;
        string? contentType = null;
        if (body is not null)
        {
            try
            {
                content = body.Text.TrimEnd('\r', '\n');
                contentType = body.ContentType.MimeType;
            }
            catch (Exception)
            {
                // the library records body_decode_failed and carries on
            }
        }

        var html = contentType is not null && contentType.Contains("html", StringComparison.OrdinalIgnoreCase);
        return (TextDirection.Detect(_subject, html: false), TextDirection.Detect(content, html));
    }
}

/// <summary>attachments: collection, TNEF unwrap, container path (ADR-004 attachments stage).</summary>
public class AttachmentsStage : StageBenchmark
{
    private TextPart? _body;

    protected override void AfterSetup() => _body = BodySelector.Select(Message.Body);

    [Benchmark]
    public IReadOnlyList<ExtractedAttachment> Collect() => PartCollector.Collect(Message.Body, _body, []);

    /// <summary>Decoded attachment bytes the stage returns for <paramref name="sample"/>; the copy-factor denominator.</summary>
    internal static long DecodedBytes(string sample)
    {
        var message = EmlExtractor.Parse(ProfileSamples.Load(sample));
        return PartCollector.Collect(message.Body, BodySelector.Select(message.Body), []).Sum(a => a.Data.LongLength);
    }
}
