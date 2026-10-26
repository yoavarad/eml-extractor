using System.Runtime.InteropServices;
using System.Text;
using MimeKit;

namespace EmlExtraction.Logics;

/// <summary>Parse stage (ADR-004): loads the input with MimeKit (bytes persistently, streams non-persistently, ADR-024), handling the mbox "From " line.</summary>
internal sealed class MimeMessageParser
{
    // Callers such as the profiling harness (ADR-020) parse without touching EmlExtractor, so register here too; the runtime ignores a repeat registration.
    static MimeMessageParser() => Encoding.RegisterProvider(CachingEncodingProvider.Instance);

    private readonly ParserOptions _options;

    /// <summary>Creates a parser.</summary>
    /// <param name="options">Options to parse with (cloned once, so later changes to the argument have no effect); null uses the lenient defaults of <see cref="CreateOptions"/>.</param>
    internal MimeMessageParser(ParserOptions? options = null)
        => _options = options?.Clone() ?? CreateOptions();

    /// <summary>Read-only stream over <paramref name="eml"/>; wraps the caller's array when it spans the whole array, else copies.</summary>
    internal static MemoryStream Open(ReadOnlyMemory<byte> eml)
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

    /// <summary>
    /// Parse stage body, without its wrapper; the profiling harness (ADR-020) calls it directly. The parse is persistent:
    /// part content is read from <paramref name="input"/> on demand, so it must stay open while the message is used.
    /// </summary>
    /// <param name="input">Stream from <see cref="Open"/> over <paramref name="eml"/>.</param>
    /// <param name="eml">The same bytes, for the mbox "From " check.</param>
    internal MimeMessage Parse(Stream input, ReadOnlySpan<byte> eml)
    {
        if (StartsWithMboxFrom(eml))
        {
            var parser = new MimeParser(_options, input, MimeFormat.Mbox, persistent: true);
            return parser.ParseMessage();
        }

        return MimeMessage.Load(_options, input, persistent: true);
    }

    /// <summary>
    /// Stream parse stage body (ADR-024): reads <paramref name="input"/> forward, non-persistently, so it need not be
    /// seekable and is not needed after this returns. The mbox "From " check peeks the first bytes through a read-ahead.
    /// </summary>
    /// <param name="input">Caller's stream (usually behind <see cref="LimitedReadStream"/>); never seeked or disposed.</param>
    internal MimeMessage Parse(Stream input)
    {
        var ahead = new ReadAheadStream(input, MboxPeekBytes);
        if (StartsWithMboxFrom(ahead.Head))
        {
            var parser = new MimeParser(_options, ahead, MimeFormat.Mbox, persistent: false);
            var message = parser.ParseMessage();

            // Only the first message is kept, but read the rest so the size limit (and telemetry) covers the whole
            // input, as on the bytes path, which checks the whole length.
            ahead.CopyTo(Stream.Null);
            return message;
        }

        return MimeMessage.Load(_options, ahead, persistent: false);
    }

    /// <summary>Optional UTF-8 BOM (3) plus "From " (5).</summary>
    private const int MboxPeekBytes = 8;

    private static ParserOptions CreateOptions()
    {
        var options = ParserOptions.Default.Clone();
        options.AllowUnquotedCommasInAddresses = true;
        options.ParameterComplianceMode = RfcComplianceMode.Loose;
        options.Rfc2047ComplianceMode = RfcComplianceMode.Loose;
        return options;
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
