using System.Diagnostics.Metrics;
using System.Reflection;

namespace EmlExtraction;

/// <summary>
/// Runtime metrics (ADR-021, ydk:req:observability/runtime-telemetry). BCL only. Every tag value is a constant from a
/// closed set or an exception type name; nothing derived from message content or the request is recorded.
/// </summary>
internal static class EmlExtractionTelemetry
{
    public const string Name = "EmlExtraction";

    public static readonly Meter Meter = new(
        Name,
        typeof(EmlExtractionTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

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
        "eml_extraction.extract.internal_errors", "{error}", "Stages that recorded internal_error.");

    private static readonly Counter<long> InputTooLarge = Meter.CreateCounter<long>(
        "eml_extraction.extract.input_too_large", "{request}", "Extract calls refused by the size guard.");

    public const string OutcomeClean = "clean";
    public const string OutcomeWarnings = "warnings";
    public const string OutcomeParseFailed = "parse_failed";
    public const string OutcomeInputTooLarge = "input_too_large";

    public static void RecordInputSize(long bytes)
    {
        if (InputSize.Enabled)
            InputSize.Record(bytes);
    }

    public static void RecordInputTooLarge()
    {
        if (InputTooLarge.Enabled)
            InputTooLarge.Add(1);
    }

    public static void RecordInternalError(string stage, Exception ex)
    {
        if (InternalErrors.Enabled)
            InternalErrors.Add(1, new("eml_extraction.stage", stage), new("error.type", ex.GetType().FullName));
    }

    public static void RecordDetect(bool supported)
    {
        if (DetectCalls.Enabled)
            DetectCalls.Add(1, new KeyValuePair<string, object?>("eml_extraction.detect.result", supported ? "supported" : "not_supported"));
    }

    /// <summary>Records duration, attachment count (when parsed) and per-code warnings for one finished Extract call.</summary>
    public static void RecordExtract(long startTimestamp, string? outcome, EmlExtractionResult result)
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
            "internal_error" => "internal_error",
            "input_too_large" => "input_too_large",
            _ => null
        };
    }
}
