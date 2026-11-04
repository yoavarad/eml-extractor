using System.Diagnostics;
using System.Text;
using EmlExtraction.Abstractions;
using EmlExtraction.Exceptions;
using EmlExtraction.Models;
using MimeKit;

namespace EmlExtraction.Logics;

/// <summary>MimeKit-based <see cref="IEmlExtractor"/>.</summary>
public sealed class EmlExtractor : IEmlExtractor
{
    static EmlExtractor() => Encoding.RegisterProvider(CachingEncodingProvider.Instance);

    private static readonly AsyncLocal<Action<string>?> Fault = new();

    private const string InternalErrorCode = "internal_error";

    /// <summary>
    /// Test seam (ADR-023, ADR-026): invoked with the stage name (detect, parse, metadata, body, attachments) inside that stage's guard,
    /// and with the metadata field points <c>metadata.&lt;key&gt;</c> and <c>metadata.header.&lt;raw key&gt;</c> inside each field guard, so a test can inject a throw. Stage names have no dot; field points do.
    /// </summary>
    internal static Action<string>? StageFault
    {
        get => Fault.Value;
        set => Fault.Value = value;
    }

    /// <summary>Default input-size limit (500 MiB), set by the project owner (not derived from measurements).</summary>
    public const long DefaultMaxInputBytes = 524_288_000;

    private readonly long _maxInputBytes;
    private readonly MimeMessageParser _parser;
    private readonly BodyExtractor _bodyExtractor = new();

    /// <summary>Creates an extractor.</summary>
    /// <param name="maxInputBytes">Largest input, in bytes, that is parsed; larger inputs make <c>Extract</c> throw.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxInputBytes"/> is not positive.</exception>
    public EmlExtractor(long maxInputBytes = DefaultMaxInputBytes)
        : this(maxInputBytes, null)
    {
    }

    /// <summary>Creates an extractor that parses with <paramref name="parserOptions"/>; internal so no MimeKit type is public (ADR-012).</summary>
    /// <param name="maxInputBytes">Largest input, in bytes, that is parsed.</param>
    /// <param name="parserOptions">MimeKit options, cloned once at construction; null uses the library's lenient defaults.</param>
    internal EmlExtractor(long maxInputBytes, ParserOptions? parserOptions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        _maxInputBytes = maxInputBytes;
        _parser = new MimeMessageParser(parserOptions);
    }

    /// <inheritdoc/>
    /// <exception cref="EmlInputTooLargeException"><paramref name="eml"/> is longer than the configured <c>maxInputBytes</c>.</exception>
    public EmlExtractionResult Extract(ReadOnlyMemory<byte> eml)
        => Extract(new EmlExtractionRequest { FileBytes = eml });

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="eml"/> is null.</exception>
    /// <exception cref="EmlInputTooLargeException">More than the configured <c>maxInputBytes</c> was read from <paramref name="eml"/>.</exception>
    public EmlExtractionResult Extract(Stream eml)
    {
        ArgumentNullException.ThrowIfNull(eml);
        return Extract(new EmlExtractionRequest { InputStream = eml });
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">Both or neither of <see cref="EmlExtractionRequest.FileBytes"/> and <see cref="EmlExtractionRequest.InputStream"/> are set.</exception>
    /// <exception cref="EmlInputTooLargeException">The input is longer than the configured <c>maxInputBytes</c>.</exception>
    public EmlExtractionResult Extract(EmlExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.HasFileBytes == request.InputStream is not null)
            throw new ArgumentException("Set exactly one of FileBytes and InputStream.", nameof(request));
        var start = Stopwatch.GetTimestamp();
        if (request.InputStream is { } stream)
            return ExtractStream(stream, start);

        var eml = request.FileBytes;
        EmlExtractionTelemetry.RecordInputSize(eml.Length);
        var activity = EmlExtractionTelemetry.StartExtract(eml.Length);
        using var scope = new EmlExtractionTelemetry.ExtractScope(activity);
        if (eml.Length > _maxInputBytes)
        {
            var tooLarge = new EmlInputTooLargeException(eml.Length, _maxInputBytes, nameof(request));
            EmlExtractionTelemetry.SetError(activity, tooLarge, EmlExtractionTelemetry.OutcomeInputTooLarge);
            EmlExtractionTelemetry.RecordInputTooLarge(start);
            throw tooLarge;
        }

        var warnings = new List<string>();

        // The parse is persistent: part content stays a view over this stream, so it lives until the result is built.
        using var input = MimeMessageParser.Open(eml);

        var message = ParseStage(input, eml, warnings, activity);
        if (message is null)
            return Recorded(start, EmlExtractionTelemetry.OutcomeParseFailed, Empty(warnings), activity);

        return Recorded(start, null, Extract(message, warnings, activity), activity);
    }

    /// <summary>Stream path (ADR-024): parses non-persistently while counting bytes; size is known only after reading.</summary>
    private EmlExtractionResult ExtractStream(Stream stream, long start)
    {
        var activity = EmlExtractionTelemetry.StartExtract(null);
        using var scope = new EmlExtractionTelemetry.ExtractScope(activity);
        var warnings = new List<string>();

        // Not disposed: disposing the wrapper must not touch the caller's stream, and it holds nothing itself.
        var input = new LimitedReadStream(stream, _maxInputBytes);
        MimeMessage? message;
        try
        {
            message = ParseStage(input, null, warnings, activity);
        }
        catch (EmlInputTooLargeException tooLarge)
        {
            EmlExtractionTelemetry.SetError(activity, tooLarge, EmlExtractionTelemetry.OutcomeInputTooLarge);
            EmlExtractionTelemetry.RecordInputTooLarge(start);
            throw;
        }
        finally
        {
            // Recorded on every path (result or refusal), as the bytes path records it up front.
            EmlExtractionTelemetry.RecordInputSize(input.BytesRead);
        }

        if (message is null)
            return Recorded(start, EmlExtractionTelemetry.OutcomeParseFailed, Empty(warnings), activity);

        return Recorded(start, null, Extract(message, warnings, activity), activity);
    }

    /// <summary>
    /// Parse stage with its guard: persistent over <paramref name="eml"/> when given, else a non-persistent stream parse.
    /// A content failure gives parse_failed; a bug gives internal_error (ADR-026); either way the result is the empty one.
    /// EmlInputTooLargeException (an ArgumentException) from the size guard passes through: it is neither a content failure nor a bug.
    /// </summary>
    private MimeMessage? ParseStage(Stream input, ReadOnlyMemory<byte>? eml, List<string> warnings, Activity? activity)
    {
        // Stage activities start and stop outside the stage try, so a host listener's exception is never caught (ADR-023).
        MimeMessage? message = null;
        var stage = EmlExtractionTelemetry.StartStage(activity, "eml_extraction.parse");
        try
        {
            try
            {
                StageFault?.Invoke("parse");
                message = eml is { } bytes ? _parser.Parse(input, bytes.Span) : _parser.Parse(input);
            }
            catch (Exception ex) when (ex is not EmlInputTooLargeException && IsContentFailure(ex))
            {
                warnings.Add($"parse_failed: {ex.GetType().Name}: {ex.Message}");
            }
            catch (Exception ex) when (IsBug(ex))
            {
                StageBug("parse", ex, warnings, stage);
            }
        }
        finally
        {
            stage?.Dispose();
        }

        return message;
    }

    /// <summary>Extracts from an already parsed <see cref="MimeMessage"/>.</summary>
    /// <param name="message">Parsed message; if it was parsed persistently, its stream must stay open until this returns.</param>
    internal EmlExtractionResult Extract(MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var start = Stopwatch.GetTimestamp();
        var activity = EmlExtractionTelemetry.StartExtract(null);
        using var scope = new EmlExtractionTelemetry.ExtractScope(activity);
        return Recorded(start, null, Extract(message, [], activity), activity);
    }

    /// <summary>The seven content exception types (ADR-023): a content catch site turns these into its warning.</summary>
    internal static bool IsContentFailure(Exception ex)
        => ex is FormatException or ParseException or IOException or InvalidOperationException or ArgumentException
               or NotSupportedException or DecoderFallbackException;

    /// <summary>Whether the detector wraps <paramref name="ex"/>: everything except OutOfMemoryException and an already wrapped exception.</summary>
    internal static bool IsWrapped(Exception ex) => ex is not (OutOfMemoryException or EmlExtractionException);

    /// <summary>Whether an Extract guard turns <paramref name="ex"/> into an internal_error warning (ADR-026): everything except OutOfMemoryException and the size guard's refusal.</summary>
    internal static bool IsBug(Exception ex) => ex is not (OutOfMemoryException or EmlInputTooLargeException);

    /// <summary>
    /// Records a library bug (ADR-026) as <c>internal_error: &lt;stage&gt;: &lt;Type&gt;</c> and counts it. The detail is the
    /// exception type name only, never its message (MimeKit messages can quote input).
    /// </summary>
    internal static void RecordBug(string stage, Exception ex, List<string> warnings)
        => RecordBug(stage, stage, ex, warnings);

    /// <summary>As <see cref="RecordBug(string, Exception, List{string})"/> with a finer warning detail (<c>metadata.&lt;key&gt;</c>); the counter is tagged with <paramref name="stage"/> only.</summary>
    internal static void RecordBug(string detail, string stage, Exception ex, List<string> warnings)
    {
        warnings.Add($"{InternalErrorCode}: {detail}: {ex.GetType().Name}");
        EmlExtractionTelemetry.RecordInternalError(stage, ex);
    }

    private static void StageBug(string stage, Exception ex, List<string> warnings, Activity? stageActivity)
    {
        EmlExtractionTelemetry.SetError(stageActivity, ex);
        RecordBug(stage, ex, warnings);
    }

    private static EmlExtractionResult Recorded(long start, string? outcome, EmlExtractionResult result, Activity? activity)
    {
        // ADR-026: a result with an internal_error warning has outcome internal_error, over warnings and parse_failed.
        if (result.Warnings.Any(w => w.StartsWith(InternalErrorCode + ":", StringComparison.Ordinal)))
            outcome = EmlExtractionTelemetry.OutcomeInternalError;
        var resolved = EmlExtractionTelemetry.RecordExtract(start, outcome, result);
        EmlExtractionTelemetry.TagExtract(activity, resolved, result);
        return result;
    }

    /// <summary>Runs the metadata, body and attachments stages; a bug in one stage gives that stage its ADR-026 default and the later stages still run.</summary>
    private EmlExtractionResult Extract(MimeMessage message, List<string> warnings, Activity? activity)
    {
        IReadOnlyDictionary<string, string> metadata;
        IReadOnlyDictionary<string, IReadOnlyList<EmailParticipant>> participants;
        var stage = EmlExtractionTelemetry.StartStage(activity, "eml_extraction.metadata");
        try
        {
            try
            {
                StageFault?.Invoke("metadata");
                metadata = HeaderMapper.Map(message, _parser.Options, warnings, out var mapped);
                participants = mapped;
            }
            catch (Exception ex) when (IsBug(ex))
            {
                StageBug("metadata", ex, warnings, stage);
                metadata = HeaderMapper.EmptyMetadata();
                participants = HeaderMapper.EmptyParticipants();
            }
        }
        finally
        {
            stage?.Dispose();
        }

        BodyExtractor.BodyContent body;
        stage = EmlExtractionTelemetry.StartStage(activity, "eml_extraction.body");
        try
        {
            try
            {
                StageFault?.Invoke("body");
                body = _bodyExtractor.ExtractBody(message, metadata.GetValueOrDefault("subject"), warnings);
            }
            catch (Exception ex) when (IsBug(ex))
            {
                StageBug("body", ex, warnings, stage);
                body = new BodyExtractor.BodyContent(null, null, null, TextDirection.Ltr, TextDirection.Ltr);
            }
        }
        finally
        {
            stage?.Dispose();
        }

        IReadOnlyList<ExtractedAttachment> attachments;
        stage = EmlExtractionTelemetry.StartStage(activity, "eml_extraction.attachments");
        try
        {
            try
            {
                StageFault?.Invoke("attachments");
                attachments = _bodyExtractor.ExtractAttachments(message, body.Part, warnings);
            }
            catch (Exception ex) when (IsBug(ex))
            {
                StageBug("attachments", ex, warnings, stage);
                attachments = [];
            }
        }
        finally
        {
            stage?.Dispose();
        }

        return new EmlExtractionResult(metadata, body.Content, body.ContentType, attachments, warnings)
        {
            SubjectDirection = body.SubjectDirection,
            ContentDirection = body.ContentDirection,
            Participants = participants
        };
    }

    private static EmlExtractionResult Empty(List<string> warnings)
        => new(HeaderMapper.EmptyMetadata(), null, null, [], warnings) { Participants = HeaderMapper.EmptyParticipants() };
}
