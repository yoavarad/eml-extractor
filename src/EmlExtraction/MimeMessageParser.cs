using System.Runtime.InteropServices;
using System.Text;
using MimeKit;

namespace EmlExtraction;

/// <summary>Parse stage (ADR-004): opens the input and loads it persistently with MimeKit, handling the mbox "From " line.</summary>
internal static class MimeMessageParser
{
    // Callers such as the profiling harness (ADR-020) parse without touching EmlExtractor, so register here too; the runtime ignores a repeat registration.
    static MimeMessageParser() => Encoding.RegisterProvider(CachingEncodingProvider.Instance);

    private static readonly ParserOptions Options = CreateOptions();

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
    internal static MimeMessage Parse(Stream input, ReadOnlySpan<byte> eml)
    {
        if (StartsWithMboxFrom(eml))
        {
            var parser = new MimeParser(Options, input, MimeFormat.Mbox, persistent: true);
            return parser.ParseMessage();
        }

        return MimeMessage.Load(Options, input, persistent: true);
    }

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
