using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using EmlExtraction.Models;

namespace EmlExtraction.Logics;

/// <summary>
/// Runtime metrics and activities (ADR-021, ydk:req:observability/runtime-telemetry). BCL only. Every tag value is a constant from a
/// closed set or an exception type name; nothing derived from message content or the request is recorded.
/// </summary>
internal static class EmlExtractionTelemetry
{
    public const string Name = "EmlExtraction";

    private static readonly string? Version =
        typeof(EmlExtractionTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    public static readonly Meter Meter = new(Name, Version);

    public static readonly ActivitySource Source = new(Name, Version);

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "eml_extraction.extract.duration", "s", "Wall time of one Extract call.");

    private static readonly Histogram<long> InputSize = Meter.CreateHistogram<long>(
        "eml_extraction.extract.input.size", "By", "Input length of one Extract call.");

    private static readonly Histogram<int> Attachments = Meter.CreateHistogram<int>(
        "eml_extraction.extract.attachments", "{attachment}", "Attachment count of one Extract call that parsed.");

    private static readonly Counter<long> Warnings = Meter.CreateCounter<long>(
        "eml_extraction.extract.warnings", "{warning}", "Warnings added to results, by code.");

    private static readonly Counter<long> DetectCalls = Meter.CreateCounter<long>(
        "eml_extraction.detect.calls", "{call}", "Detect and IsEml calls, by result.");

    private static readonly Counter<long> InternalErrors = Meter.CreateCounter<long>(
        "eml_extraction.extract.internal_errors", "{error}", "Extract calls that threw EmlExtractionException, by stage.");

    private static readonly Counter<long> InputTooLarge = Meter.CreateCounter<long>(
        "eml_extraction.extract.input_too_large", "{request}", "Extract calls refused by the size guard.");

    public const string OutcomeClean = "clean";
    public const string OutcomeWarnings = "warnings";
    public const string OutcomeParseFailed = "parse_failed";
    public const string OutcomeInputTooLarge = "input_too_large";
    public const string OutcomeInternalError = "internal_error";

    /// <summary>Starts the eml_extraction.extract activity; null when no listener samples it. Tags input.size (when known) only when all data is requested.</summary>
    public static Activity? StartExtract(long? inputBytes)
    {
        var previous = Activity.Current;
        Activity? activity;
        try
        {
            activity = Source.StartActivity("eml_extraction.extract", ActivityKind.Internal);
        }
        catch
        {
            // A host ActivityStarted callback threw after Start made the activity current; do not leak it.
            Activity.Current = previous;
            throw;
        }

        if (inputBytes is { } bytes && activity is { IsAllDataRequested: true })
            activity.SetTag("eml_extraction.input.size", bytes);
        return activity;
    }

    /// <summary>Stops the extract activity; restores Activity.Current to its parent if a host ActivityStopped callback throws.</summary>
    public readonly struct ExtractScope(Activity? activity) : IDisposable
    {
        public void Dispose()
        {
            if (activity is null)
                return;
            var parent = activity.Parent;
            try
            {
                activity.Dispose();
            }
            catch
            {
                Activity.Current = parent;
                throw;
            }
        }
    }

    /// <summary>Starts a stage child activity (name is one of the four eml_extraction.&lt;stage&gt; constants) only when the parent requests all data.</summary>
    public static Activity? StartStage(Activity? parent, string name)
        => parent is { IsAllDataRequested: true } ? Source.StartActivity(name, ActivityKind.Internal) : null;

    /// <summary>Sets outcome and counts on the extract activity of a call that returned a result.</summary>
    public static void TagExtract(Activity? activity, string outcome, EmlExtractionResult result)
    {
        if (activity is not { IsAllDataRequested: true })
            return;
        activity.SetTag("eml_extraction.attachments.count", result.Attachments.Count);
        activity.SetTag("eml_extraction.warnings.count", result.Warnings.Count);
        activity.SetTag("eml_extraction.outcome", outcome);
    }

    /// <summary>Marks an activity as failed: status Error (no description) and error.type; the exception message is never recorded.</summary>
    public static void SetError(Activity? activity, Exception ex, string? outcome = null)
    {
        if (activity is not { IsAllDataRequested: true })
            return;
        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag("error.type", ex.GetType().FullName);
        if (outcome is not null)
            activity.SetTag("eml_extraction.outcome", outcome);
    }

    public static void RecordInputSize(long bytes)
    {
        if (InputSize.Enabled)
            InputSize.Record(bytes);
    }

    /// <summary>Records the refusal counter and a duration with outcome input_too_large; nothing goes to the warnings counter.</summary>
    public static void RecordInputTooLarge(long startTimestamp)
    {
        if (InputTooLarge.Enabled)
            InputTooLarge.Add(1);

        if (Duration.Enabled)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            Duration.Record(elapsed, new KeyValuePair<string, object?>("eml_extraction.outcome", OutcomeInputTooLarge));
        }
    }

    /// <summary>Records the internal-error counter (stage, inner exception type) and a duration with outcome internal_error, for a call that throws EmlExtractionException.</summary>
    public static void RecordInternalError(string stage, Exception inner, long startTimestamp)
    {
        if (InternalErrors.Enabled)
            InternalErrors.Add(1, new("eml_extraction.stage", stage), new("error.type", inner.GetType().FullName));

        if (Duration.Enabled)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            Duration.Record(elapsed, new KeyValuePair<string, object?>("eml_extraction.outcome", OutcomeInternalError));
        }
    }

    public static void RecordDetect(bool supported)
    {
        if (DetectCalls.Enabled)
            DetectCalls.Add(1, new KeyValuePair<string, object?>("eml_extraction.detect.result", supported ? "supported" : "not_supported"));
    }

    /// <summary>Records duration, attachment count (when parsed) and per-code warnings for one finished Extract call; returns the resolved outcome.</summary>
    public static string RecordExtract(long startTimestamp, string? outcome, EmlExtractionResult result)
    {
        if (Warnings.Enabled)
        {
            foreach (var warning in result.Warnings)
            {
                var code = Code(warning);
                if (code is not null)
                    Warnings.Add(1, new KeyValuePair<string, object?>("eml_extraction.warning.code", code));
            }
        }

        outcome ??= result.Warnings.Count == 0 ? OutcomeClean : OutcomeWarnings;
        if ((outcome is OutcomeClean or OutcomeWarnings) && Attachments.Enabled)
            Attachments.Record(result.Attachments.Count);

        if (Duration.Enabled)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            Duration.Record(elapsed, new KeyValuePair<string, object?>("eml_extraction.outcome", outcome));
        }

        return outcome;
    }

    private static string? Code(string warning)
    {
        var colon = warning.IndexOf(':');
        var prefix = colon < 0 ? warning.AsSpan() : warning.AsSpan(0, colon);
        return prefix switch
        {
            "parse_failed" => "parse_failed",
            "body_decode_failed" => "body_decode_failed",
            "part_decode_failed" => "part_decode_failed",
            "rfc822_extract_failed" => "rfc822_extract_failed",
            "tnef_extract_failed" => "tnef_extract_failed",
            _ => null
        };
    }
}
