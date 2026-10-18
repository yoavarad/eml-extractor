using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MimeKit;

namespace EmlExtraction;

/// <summary>MimeKit-based <see cref="IEmlExtractor"/>.</summary>
public sealed class EmlExtractor : IEmlExtractor
{
    static EmlExtractor() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static readonly ParserOptions Options = CreateOptions();

    private static readonly AsyncLocal<Action<string>?> Fault = new();

    /// <summary>Test seam (ADR-004): invoked with the stage name (parse, metadata, body, attachments) inside that stage's guard, so a test can inject a throw.</summary>
    internal static Action<string>? StageFault
    {
        get => Fault.Value;
        set => Fault.Value = value;
    }

    /// <summary>Provisional default input-size limit (150 MiB).</summary>
    public const long DefaultMaxInputBytes = 157_286_400;

    private readonly long _maxInputBytes;

    /// <summary>Creates an extractor.</summary>
    /// <param name="maxInputBytes">Largest input, in bytes, that is parsed; larger inputs yield an <c>input_too_large</c> warning.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxInputBytes"/> is not positive.</exception>
    public EmlExtractor(long maxInputBytes = DefaultMaxInputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        _maxInputBytes = maxInputBytes;
    }

    /// <inheritdoc/>
    public EmlExtractionResult Extract(ReadOnlyMemory<byte> eml)
        => Extract(new EmlExtractionRequest { FileBytes = eml });

    /// <inheritdoc/>
    public EmlExtractionResult Extract(EmlExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var start = Stopwatch.GetTimestamp();
        var eml = request.FileBytes;
        EmlExtractionTelemetry.RecordInputSize(eml.Length);
        var warnings = new List<string>();
        if (eml.Length > _maxInputBytes)
        {
            warnings.Add($"input_too_large: {eml.Length} > {_maxInputBytes}");
            EmlExtractionTelemetry.RecordInputTooLarge();
            return Recorded(start, EmlExtractionTelemetry.OutcomeInputTooLarge, Empty(warnings));
        }

        MimeMessage message;
        try
        {
            StageFault?.Invoke("parse");
            using var stream = Open(eml);
            if (StartsWithMboxFrom(eml.Span))
            {
                var parser = new MimeParser(Options, stream, MimeFormat.Mbox);
                message = parser.ParseMessage();
            }
            else
            {
                message = MimeMessage.Load(Options, stream);
            }
        }
        catch (Exception ex) when (ex is FormatException or ParseException or IOException
                                       or InvalidOperationException or ArgumentException
                                       or NotSupportedException or DecoderFallbackException)
        {
            warnings.Add($"parse_failed: {ex.GetType().Name}: {ex.Message}");
            return Recorded(start, EmlExtractionTelemetry.OutcomeParseFailed, Empty(warnings));
        }
        catch (Exception ex)
        {
            warnings.Add(InternalError("parse", ex));
            return Recorded(start, EmlExtractionTelemetry.OutcomeParseFailed, Empty(warnings));
        }

        return Recorded(start, null, Extract(message, warnings));
    }

    /// <summary>Extracts from an already parsed <see cref="MimeMessage"/>.</summary>
    /// <param name="message">Parsed message.</param>
    internal EmlExtractionResult Extract(MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var start = Stopwatch.GetTimestamp();
        return Recorded(start, null, Extract(message, []));
    }

    private static EmlExtractionResult Recorded(long start, string? outcome, EmlExtractionResult result)
    {
        EmlExtractionTelemetry.RecordExtract(start, outcome, result);
        return result;
    }

    internal static EmlExtractionResult Extract(MimeMessage message, List<string> warnings)
    {
        IReadOnlyDictionary<string, string> metadata;
        try
        {
            StageFault?.Invoke("metadata");
            metadata = HeaderMapper.Map(message);
        }
        catch (Exception ex)
        {
            metadata = HeaderMapper.EmptyMetadata();
            warnings.Add(InternalError("metadata", ex));
        }

        TextPart? body = null;
        string? content = null;
        string? contentType = null;
        var subjectDir = "ltr";
        var contentDir = "ltr";
        try
        {
            body = BodySelector.Select(message.Body);
            StageFault?.Invoke("body");
            if (body is not null)
            {
                try
                {
                    content = body.Text.TrimEnd('\r', '\n');
                    contentType = body.ContentType.MimeType;
                }
                catch (Exception ex)
                {
                    warnings.Add($"body_decode_failed: {ex.Message}");
                }
            }

            var html = contentType is not null
                       && contentType.Contains("html", StringComparison.OrdinalIgnoreCase);
            subjectDir = TextDirection.Detect(metadata.GetValueOrDefault("subject"), html: false);
            contentDir = TextDirection.Detect(content, html);
        }
        catch (Exception ex)
        {
            content = null;
            contentType = null;
            subjectDir = "ltr";
            contentDir = "ltr";
            warnings.Add(InternalError("body", ex));
        }

        IReadOnlyList<ExtractedAttachment> attachments;
        try
        {
            StageFault?.Invoke("attachments");
            attachments = PartCollector.Collect(message.Body, body, warnings);
        }
        catch (Exception ex)
        {
            attachments = [];
            warnings.Add(InternalError("attachments", ex));
        }

        return new EmlExtractionResult(metadata, content, contentType, attachments, warnings)
        {
            SubjectDirection = subjectDir,
            ContentDirection = contentDir
        };
    }

    private static string InternalError(string stage, Exception ex)
    {
        EmlExtractionTelemetry.RecordInternalError(stage, ex);
        return $"internal_error: {stage}: {ex.GetType().Name}: {ex.Message}";
    }

    private static EmlExtractionResult Empty(List<string> warnings)
        => new(HeaderMapper.EmptyMetadata(), null, null, [], warnings);

    private static ParserOptions CreateOptions()
    {
        var options = ParserOptions.Default.Clone();
        options.AllowUnquotedCommasInAddresses = true;
        options.ParameterComplianceMode = RfcComplianceMode.Loose;
        options.Rfc2047ComplianceMode = RfcComplianceMode.Loose;
        return options;
    }

    private static Stream Open(ReadOnlyMemory<byte> eml)
    {
        if (MemoryMarshal.TryGetArray(eml, out var segment)
            && segment.Array is not null
            && segment.Offset == 0
            && segment.Count == eml.Length)
        {
            return new MemoryStream(segment.Array, writable: false);
        }

        return new MemoryStream(eml.ToArray(), writable: false);
    }

    private static bool StartsWithMboxFrom(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            data = data[3..];
        return data.Length >= 5
               && data[0] == (byte)'F'
               && data[1] == (byte)'r'
               && data[2] == (byte)'o'
               && data[3] == (byte)'m'
               && data[4] == (byte)' ';
    }
}
