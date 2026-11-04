using System.Diagnostics.Metrics;
using EmlExtraction.Logics;
using MimeKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-026: <c>eml_extraction.extract.internal_errors</c> counts internal_error warnings, not throws; the call's duration is recorded once, when it returns.</summary>
[Collection(TelemetryCollectionDefinition.Name)]
public sealed class EmlExtractionTelemetryTests
{
    private const string InternalErrors = "eml_extraction.extract.internal_errors";
    private const string Duration = "eml_extraction.extract.duration";

    [Fact]
    public void RecordInternalErrorCountsOnceWithoutDuration()
    {
        var seen = Listen(() => EmlExtractionTelemetry.RecordInternalError("attachments", new InvalidCastException("x")));

        var (name, value, tags) = Assert.Single(seen);
        Assert.Equal(InternalErrors, name);
        Assert.Equal(1L, value);
        Assert.Equal(
            ["eml_extraction.stage=attachments", $"error.type={typeof(InvalidCastException).FullName}"],
            tags);
    }

    [Fact]
    public void EachPartBugCountsAndTheCallRecordsOneInternalErrorDuration()
    {
        var message = SampleFactory.Plain("s", "t");
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "hi" },
            Bad("a.bin"),
            Bad("b.bin")
        };

        var seen = Listen(() => new EmlExtractor().Extract(message));

        Assert.Equal(2, seen.Count(m => m.Name == InternalErrors));
        var duration = Assert.Single(seen, m => m.Name == Duration);
        Assert.Equal(["eml_extraction.outcome=internal_error"], duration.Tags);
    }

    private static MimePart Bad(string name)
        => new("application", "octet-stream") { FileName = name, Content = new MimeContent(new BugStream()) };

    private static List<(string Name, object Value, string[] Tags)> Listen(Action run)
    {
        // The listener is process-wide and other test classes extract in parallel; keep only this thread's measurements
        // (instruments record synchronously on the calling thread).
        var thread = Environment.CurrentManagedThreadId;
        var seen = new List<(string, object, string[])>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "EmlExtraction")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((i, v, t, _) => Add(seen, thread, i.Name, v, t));
        listener.SetMeasurementEventCallback<double>((i, v, t, _) => Add(seen, thread, i.Name, v, t));
        listener.SetMeasurementEventCallback<int>((i, v, t, _) => Add(seen, thread, i.Name, v, t));
        listener.Start();
        run();
        return seen;
    }

    private static void Add(List<(string, object, string[])> seen, int thread, string name, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (Environment.CurrentManagedThreadId != thread)
            return;
        var flat = new string[tags.Length];
        for (var i = 0; i < tags.Length; i++)
            flat[i] = $"{tags[i].Key}={tags[i].Value}";
        lock (seen)
            seen.Add((name, value, flat));
    }

    /// <summary>A content stream whose read throws a non-content exception (a library bug).</summary>
    private sealed class BugStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 10;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidCastException("bug");
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
