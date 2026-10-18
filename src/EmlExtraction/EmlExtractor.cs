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

    /// <summary>Test seam (ADR-023): invoked with the stage name (detect, parse, metadata, body, attachments) inside that stage's wrapper, so a test can inject a throw.</summary>
    internal static Action<string>? StageFault
    {
        get => Fault.Value;
        set => Fault.Value = value;
    }

    /// <summary>Default input-size limit (500 MiB), set by the project owner (not derived from measurements).</summary>
    public const long DefaultMaxInputBytes = 524_288_000;

    private readonly long _maxInputBytes;

    /// <summary>Creates an extractor.</summary>
    /// <param name="maxInputBytes">Largest input, in bytes, that is parsed; larger inputs make <c>Extract</c> throw.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxInputBytes"/> is not positive.</exception>
    public EmlExtractor(long maxInputBytes = DefaultMaxInputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        _maxInputBytes = maxInputBytes;
    }

    /// <inheritdoc/>
    /// <exception cref="EmlInputTooLargeException"><paramref name="eml"/> is longer than the configured <c>maxInputBytes</c>.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    public EmlExtractionResult Extract(ReadOnlyMemory<byte> eml)
        => Extract(new EmlExtractionRequest { FileBytes = eml });

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="EmlInputTooLargeException"><see cref="EmlExtractionRequest.FileBytes"/> is longer than the configured <c>maxInputBytes</c>.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    public EmlExtractionResult Extract(EmlExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var start = Stopwatch.GetTimestamp();
        var eml = request.FileBytes;
        EmlExtractionTelemetry.RecordInputSize(eml.Length);
        if (eml.Length > _maxInputBytes)
        {
            EmlExtractionTelemetry.RecordInputTooLarge(start);
            throw new EmlInputTooLargeException(eml.Length, _maxInputBytes, nameof(request));
        }

        var warnings = new List<string>();

        MimeMessage message;
        try
        {
            StageFault?.Invoke("parse");
            message = Parse(eml);
        }
        catch (Exception ex) when (IsContentFailure(ex))
        {
            warnings.Add($"parse_failed: {ex.GetType().Name}: {ex.Message}");
            return Recorded(start, EmlExtractionTelemetry.OutcomeParseFailed, Empty(warnings));
        }
        catch (Exception ex) when (IsWrapped(ex))
        {
            throw Wrap("parse", ex, start);
        }

        return Recorded(start, null, Extract(message, warnings, start));
    }

    /// <summary>Extracts from an already parsed <see cref="MimeMessage"/>.</summary>
    /// <param name="message">Parsed message.</param>
    internal EmlExtractionResult Extract(MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var start = Stopwatch.GetTimestamp();
        return Recorded(start, null, Extract(message, [], start));
    }

    /// <summary>The seven content exception types (ADR-023): a content catch site turns these into its warning.</summary>
    internal static bool IsContentFailure(Exception ex)
        => ex is FormatException or ParseException or IOException or InvalidOperationException or ArgumentException
               or NotSupportedException or DecoderFallbackException;

    /// <summary>Whether a stage wrapper wraps <paramref name="ex"/>: everything except OutOfMemoryException and an already wrapped exception.</summary>
    internal static bool IsWrapped(Exception ex) => ex is not (OutOfMemoryException or EmlExtractionException);

    private static EmlExtractionException Wrap(string stage, Exception ex, long start)
    {
        EmlExtractionTelemetry.RecordInternalError(stage, ex, start);
        return new EmlExtractionException(stage, ex);
    }

    private static EmlExtractionResult Recorded(long start, string? outcome, EmlExtractionResult result)
    {
        EmlExtractionTelemetry.RecordExtract(start, outcome, result);
        return result;
    }

    /// <summary>Parse stage body, without its wrapper; the profiling harness (ADR-020) calls it directly.</summary>
    internal static MimeMessage Parse(ReadOnlyMemory<byte> eml)
    {
        using var stream = Open(eml);
        if (StartsWithMboxFrom(eml.Span))
        {
            var parser = new MimeParser(Options, stream, MimeFormat.Mbox);
            return parser.ParseMessage();
        }

        return MimeMessage.Load(Options, stream);
    }

    private static EmlExtractionResult Extract(MimeMessage message, List<string> warnings, long start)
    {
        IReadOnlyDictionary<string, string> metadata;
        try
        {
            StageFault?.Invoke("metadata");
            metadata = HeaderMapper.Map(message);
        }
        catch (Exception ex) when (IsWrapped(ex))
        {
            throw Wrap("metadata", ex, start);
        }

        TextPart? body;
        string? content = null;
        string? contentType = null;
        string subjectDir;
        string contentDir;
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
                catch (Exception ex) when (IsContentFailure(ex))
                {
                    warnings.Add($"body_decode_failed: {ex.Message}");
                }
            }

            var html = contentType is not null
                       && contentType.Contains("html", StringComparison.OrdinalIgnoreCase);
            subjectDir = TextDirection.Detect(metadata.GetValueOrDefault("subject"), html: false);
            contentDir = TextDirection.Detect(content, html);
        }
        catch (Exception ex) when (IsWrapped(ex))
        {
            throw Wrap("body", ex, start);
        }

        IReadOnlyList<ExtractedAttachment> attachments;
        try
        {
            StageFault?.Invoke("attachments");
            attachments = PartCollector.Collect(message.Body, body, warnings);
        }
        catch (Exception ex) when (IsWrapped(ex))
        {
            throw Wrap("attachments", ex, start);
        }

        return new EmlExtractionResult(metadata, content, contentType, attachments, warnings)
        {
            SubjectDirection = subjectDir,
            ContentDirection = contentDir
        };
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
