using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using MimeKit;

namespace EmlExtraction.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryCollection
{
    public const string Name = "Telemetry";
}

/// <summary>ydk:test:observability/telemetry.unit, metric cases (ADR-021). Real MeterListener on meter "EmlExtraction".</summary>
[Collection(TelemetryCollection.Name)]
public sealed class TelemetryTests
{
    private const string Duration = "eml_extraction.extract.duration";
    private const string InputSize = "eml_extraction.extract.input.size";
    private const string Attachments = "eml_extraction.extract.attachments";
    private const string Warnings = "eml_extraction.extract.warnings";
    private const string DetectCalls = "eml_extraction.detect.calls";
    private const string InternalErrors = "eml_extraction.extract.internal_errors";
    private const string InputTooLarge = "eml_extraction.extract.input_too_large";

    private static readonly Dictionary<string, HashSet<string>> AllowedTags = new()
    {
        ["eml_extraction.outcome"] = ["clean", "warnings", "parse_failed", "input_too_large"],
        ["eml_extraction.warning.code"] =
        [
            "parse_failed", "body_decode_failed", "part_decode_failed", "rfc822_extract_failed",
            "tnef_extract_failed", "internal_error"
        ],
        ["eml_extraction.detect.result"] = ["supported", "not_supported"],
        ["eml_extraction.stage"] = ["parse", "metadata", "body", "attachments"],
        ["error.type"] = [typeof(InvalidCastException).FullName!]
    };

    private readonly EmlExtractor _extractor = new();

    [Fact]
    public void MeterExposesExactInstrumentSet()
    {
        using var rec = new Recorder();
        _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("s", "t")));
        new EmlDetector().Detect(SampleFactory.ToBytes(SampleFactory.Plain("s", "t")));

        var expected = new (string Name, Type Kind, string Unit)[]
        {
            (Duration, typeof(Histogram<double>), "s"),
            (InputSize, typeof(Histogram<long>), "By"),
            (Attachments, typeof(Histogram<int>), "{attachment}"),
            (Warnings, typeof(Counter<long>), "{warning}"),
            (DetectCalls, typeof(Counter<long>), "{call}"),
            (InternalErrors, typeof(Counter<long>), "{error}"),
            (InputTooLarge, typeof(Counter<long>), "{request}")
        };
        var actual = rec.Instruments.Select(i => (i.Name, i.GetType(), i.Unit ?? "")).OrderBy(i => i.Name, StringComparer.Ordinal);
        Assert.Equal(expected.OrderBy(i => i.Name, StringComparer.Ordinal), actual);

        var meter = rec.Instruments[0].Meter;
        var informational = typeof(EmlExtractor).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.Equal(informational, meter.Version);
    }

    [Fact]
    public void CleanMessageRecordsDurationSizeAttachments()
    {
        var builder = new BodyBuilder { TextBody = "hello" };
        builder.Attachments.Add("a.bin", [1, 2, 3], new ContentType("application", "octet-stream"));
        builder.Attachments.Add("b.bin", [4, 5, 6], new ContentType("application", "octet-stream"));
        var message = SampleFactory.Plain("s", "t");
        message.Body = builder.ToMessageBody();
        var bytes = SampleFactory.ToBytes(message);

        using var rec = new Recorder();
        var result = _extractor.Extract(bytes);

        Assert.Empty(result.Warnings);
        var duration = Assert.Single(rec.Of(Duration));
        Assert.True(duration.Value > 0);
        Assert.Equal(Tag("eml_extraction.outcome", "clean"), Assert.Single(duration.Tags));
        Assert.Equal((double)bytes.Length, Assert.Single(rec.Of(InputSize)).Value);
        Assert.Equal(2d, Assert.Single(rec.Of(Attachments)).Value);
        Assert.Empty(rec.Of(Warnings));
        Assert.Empty(rec.Of(InternalErrors));
        Assert.Empty(rec.Of(InputTooLarge));
    }

    [Fact]
    public void WarningsCountedByCode()
    {
        var message = SampleFactory.Plain("s", "t");
        var bad = new MimePart("application", "octet-stream")
        {
            FileName = "bad.bin",
            Content = new MimeContent(new ThrowingStream())
        };
        message.Body = new Multipart("mixed") { new TextPart("plain") { Text = "hi" }, bad };

        using var rec = new Recorder();
        var result = _extractor.Extract(message);

        Assert.StartsWith("part_decode_failed", Assert.Single(result.Warnings), StringComparison.Ordinal);
        var warning = Assert.Single(rec.Of(Warnings));
        Assert.Equal(1d, warning.Value);
        Assert.Equal(Tag("eml_extraction.warning.code", "part_decode_failed"), Assert.Single(warning.Tags));
        Assert.Equal(Tag("eml_extraction.outcome", "warnings"), Assert.Single(Assert.Single(rec.Of(Duration)).Tags));
    }

    [Fact]
    public void InputTooLargeCounted()
    {
        var bytes = SampleFactory.ToBytes(SampleFactory.Plain("s", new string('x', 2000)));

        using var rec = new Recorder();
        Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(1024).Extract(bytes));

        Assert.Equal(1d, Assert.Single(rec.Of(InputTooLarge)).Value);
        Assert.Empty(rec.Of(Warnings));
        Assert.Equal(Tag("eml_extraction.outcome", "input_too_large"), Assert.Single(Assert.Single(rec.Of(Duration)).Tags));
        Assert.Equal((double)bytes.Length, Assert.Single(rec.Of(InputSize)).Value);
        Assert.Empty(rec.Of(Attachments));
    }

    [Fact]
    public void ParseFailureTaggedParseFailed()
    {
        using var rec = new Recorder();
        EmlExtractionResult result;
        EmlExtractor.StageFault = s =>
        {
            if (s == "parse")
                throw new FormatException("x");
        };
        try
        {
            result = _extractor.Extract(SampleFactory.ToBytes(SampleFactory.Plain("s", "t")));
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }

        Assert.StartsWith("parse_failed", Assert.Single(result.Warnings), StringComparison.Ordinal);
        Assert.Equal(Tag("eml_extraction.outcome", "parse_failed"), Assert.Single(Assert.Single(rec.Of(Duration)).Tags));
        Assert.Equal(Tag("eml_extraction.warning.code", "parse_failed"), Assert.Single(Assert.Single(rec.Of(Warnings)).Tags));
        Assert.Empty(rec.Of(Attachments));
    }

    [Fact]
    public void InternalErrorCountedByStage()
    {
        var bytes = SampleFactory.ToBytes(SampleFactory.Plain("s", "t"));

        using var rec = new Recorder();
        EmlExtractor.StageFault = s =>
        {
            if (s == "attachments")
                throw new InvalidCastException("boom");
        };
        try
        {
            _extractor.Extract(bytes);
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }

        var error = Assert.Single(rec.Of(InternalErrors));
        Assert.Equal(1d, error.Value);
        Assert.Equal(
            [Tag("eml_extraction.stage", "attachments"), Tag("error.type", typeof(InvalidCastException).FullName)],
            error.Tags.OrderBy(t => t.Key, StringComparer.Ordinal));
        Assert.Equal(Tag("eml_extraction.warning.code", "internal_error"), Assert.Single(Assert.Single(rec.Of(Warnings)).Tags));
        Assert.Equal(Tag("eml_extraction.outcome", "warnings"), Assert.Single(Assert.Single(rec.Of(Duration)).Tags));
    }

    [Fact]
    public void ParseStageInternalErrorTaggedParseFailed()
    {
        using var rec = new Recorder();
        EmlExtractor.StageFault = s =>
        {
            if (s == "parse")
                throw new InvalidCastException("boom");
        };
        try
        {
            _extractor.Extract(new EmlExtractionRequest { FileBytes = SampleFactory.ToBytes(SampleFactory.Plain("s", "t")) });
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }

        Assert.Contains(Tag("eml_extraction.stage", "parse"), Assert.Single(rec.Of(InternalErrors)).Tags);
        Assert.Equal(Tag("eml_extraction.warning.code", "internal_error"), Assert.Single(Assert.Single(rec.Of(Warnings)).Tags));
        Assert.Equal(Tag("eml_extraction.outcome", "parse_failed"), Assert.Single(Assert.Single(rec.Of(Duration)).Tags));
        Assert.Empty(rec.Of(Attachments));
    }

    [Fact]
    public void DetectCountedByResult()
    {
        var mail = SampleFactory.ToBytes(SampleFactory.Plain("s", "t"));
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ\n1 0 obj << >> endobj\n");
        var detector = new EmlDetector();

        using var rec = new Recorder();
        detector.Detect(mail);
        detector.Detect(pdf);
        detector.IsEml(mail);

        var calls = rec.Of(DetectCalls);
        Assert.Equal(3, calls.Count);
        Assert.Equal(2d, calls.Where(c => c.Tags.Single().Value is "supported").Sum(c => c.Value));
        Assert.Equal(1d, calls.Where(c => c.Tags.Single().Value is "not_supported").Sum(c => c.Value));
    }

    [Fact]
    public void NoPiiInTelemetry()
    {
        const string marker = "ZQXPIIMARKER";
        var message = SampleFactory.Plain(
            $"subject {marker}",
            "unused",
            $"{marker} <from{marker}@example.com>",
            $"{marker} <to{marker}@example.com>");
        message.Cc.Add(MailboxAddress.Parse($"{marker} <cc{marker}@example.com>"));
        message.Bcc.Add(MailboxAddress.Parse($"{marker} <bcc{marker}@example.com>"));
        message.ReplyTo.Add(MailboxAddress.Parse($"rt{marker}@example.com"));
        message.Headers.Add("X-Custom", marker);
        var builder = new BodyBuilder { TextBody = $"body {marker}" };
        var att = builder.Attachments.Add($"{marker}.bin", [1, 2, 3], new ContentType("application", "octet-stream"));
        att.ContentId = $"{marker}@cid";
        att.ContentLocation = new Uri($"http://example.com/{marker}");
        message.Body = builder.ToMessageBody();
        var bytes = SampleFactory.ToBytes(message);

        using var rec = new Recorder();
        _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes, FileName = $"{marker}.eml" });
        new EmlDetector().Detect(bytes);
        EmlExtractor.StageFault = s =>
        {
            if (s == "metadata")
                throw new InvalidCastException(marker);
        };
        try
        {
            _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes, FileName = $"{marker}.eml" });
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }

        Assert.NotEmpty(rec.Measurements);
        foreach (var m in rec.Measurements)
        {
            foreach (var tag in m.Tags)
            {
                Assert.True(AllowedTags.TryGetValue(tag.Key, out var values), $"unlisted tag key {tag.Key}");
                var value = Assert.IsType<string>(tag.Value);
                Assert.DoesNotContain(marker, value, StringComparison.Ordinal);
                Assert.Contains(value, values);
            }
        }
    }

    [Fact]
    public void ResultIdenticalWithAndWithoutListener()
    {
        var bytes = SampleFactory.ToBytes(SampleFactory.Plain("שלום", "hello"));
        var without = ResultProjection.Of(_extractor.Extract(bytes));

        using var rec = new Recorder();
        var with = ResultProjection.Of(_extractor.Extract(bytes));

        Assert.Equal(without, with);
    }

    [Fact]
    public void LibraryReferencesNoTelemetryPackages()
    {
        var refs = typeof(EmlExtractor).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(refs, n => n.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase)
                                         || n.StartsWith("Microsoft.Extensions.Logging", StringComparison.OrdinalIgnoreCase)
                                         || n.Equals("System.Diagnostics.Tracing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(EmlExtractor).Assembly.GetTypes(),
            t => typeof(System.Diagnostics.Tracing.EventSource).IsAssignableFrom(t));
    }

    private static KeyValuePair<string, object?> Tag(string key, string? value) => new(key, value);

    internal sealed record Measurement(string Instrument, double Value, KeyValuePair<string, object?>[] Tags);

    private sealed class Recorder : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<Instrument> Instruments { get; } = [];

        public List<Measurement> Measurements { get; } = [];

        public Recorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != "EmlExtraction")
                    return;
                lock (Instruments)
                    Instruments.Add(instrument);
                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>((i, v, t, _) => Add(i, v, t));
            _listener.SetMeasurementEventCallback<long>((i, v, t, _) => Add(i, v, t));
            _listener.SetMeasurementEventCallback<int>((i, v, t, _) => Add(i, v, t));
            _listener.Start();
        }

        public List<Measurement> Of(string name)
        {
            lock (Measurements)
                return Measurements.Where(m => m.Instrument == name).ToList();
        }

        public void Dispose() => _listener.Dispose();

        private void Add(Instrument i, double v, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copy = tags.ToArray();
            lock (Measurements)
                Measurements.Add(new Measurement(i.Name, v, copy));
        }
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 10;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("unreadable");
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
