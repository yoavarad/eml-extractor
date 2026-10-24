using EmlExtraction.Abstractions;
using EmlExtraction.Exceptions;
using EmlExtraction.Models;

namespace EmlExtraction.Logics;

/// <summary>Header-sniffing <see cref="IEmlDetector"/>: inspects a bounded prefix for RFC 5322 header lines.</summary>
public sealed class EmlDetector : IEmlDetector
{
    private const int MinLength = 20;
    private const int SniffLimit = 16 * 1024;

    private static readonly string[] ClassicHeaders =
    [
        "from", "date", "received", "mime-version", "message-id", "subject",
        "return-path", "to", "delivered-to", "cc", "sender", "reply-to",
        "in-reply-to", "references", "content-type"
    ];

    private static readonly EmlDetectionResult Supported = new(true, "message/rfc822");
    private static readonly EmlDetectionResult Unsupported = new(false, "");

    /// <inheritdoc/>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred during detection; stage <c>detect</c>.</exception>
    public bool IsEml(ReadOnlySpan<byte> data) => Counted(data);

    /// <summary>Detects whether <paramref name="data"/> is a mail message; input that is not a message gives an unsupported result.</summary>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred during detection; stage <c>detect</c>.</exception>
    public EmlDetectionResult Detect(ReadOnlySpan<byte> data)
        => Counted(data) ? Supported : Unsupported;

    private static bool Counted(ReadOnlySpan<byte> data)
    {
        bool supported;
        try
        {
            EmlExtractor.StageFault?.Invoke("detect");
            supported = Sniff(data);
        }
        catch (Exception ex) when (EmlExtractor.IsWrapped(ex))
        {
            throw new EmlExtractionException("detect", ex);
        }

        EmlExtractionTelemetry.RecordDetect(supported);
        return supported;
    }

    private static bool Sniff(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinLength)
            return false;

        var span = StripUtf8Bom(data);
        if (LooksLikeNonEmail(span))
            return false;

        var window = span.Length > SniffLimit ? span[..SniffLimit] : span;
        if (StartsWithAscii(window, "From "))
        {
            var newline = window.IndexOf((byte)'\n');
            if (newline <= 0 || newline + 1 >= window.Length)
                return false;
            window = window[(newline + 1)..];
        }

        return LooksLikeHeaderBlock(window);
    }

    private static ReadOnlySpan<byte> StripUtf8Bom(ReadOnlySpan<byte> data)
        => data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF
            ? data[3..]
            : data;

    private static bool LooksLikeNonEmail(ReadOnlySpan<byte> span)
        => HasMagic(span, [0xD0, 0xCF, 0x11, 0xE0])
           || StartsWithAscii(span, "%PDF")
           || HasMagic(span, [0x89, 0x50, 0x4E, 0x47])
           || HasMagic(span, [0xFF, 0xD8, 0xFF])
           || (span.Length >= 4 && span[0] == (byte)'P' && span[1] == (byte)'K' && span[2] <= 0x08)
           || span[0] is (byte)'{' or (byte)'[' or (byte)'<';

    // Walks the window line by line (CRLF or LF) with no string per line; same rules and stop points as the
    // earlier decode-and-split form (ASCII decode turned every byte >= 0x80 into '?', never a name char).
    private static bool LooksLikeHeaderBlock(ReadOnlySpan<byte> window)
    {
        var headerLines = 0;
        var classic = 0;
        var seenBlank = false;
        var rest = window;

        while (true)
        {
            var newline = rest.IndexOf((byte)'\n');
            var last = newline < 0;
            ReadOnlySpan<byte> line;
            if (last)
            {
                line = rest;
            }
            else
            {
                line = rest[..newline];
                rest = rest[(newline + 1)..];
                if (line.Length > 0 && line[^1] == (byte)'\r')
                    line = line[..^1];
            }

            if (line.Length == 0)
            {
                seenBlank = headerLines > 0;
                break;
            }

            if (line[0] is (byte)' ' or (byte)'\t')
            {
                if (headerLines == 0)
                    return false;
                headerLines++;
            }
            else
            {
                var colon = line.IndexOf((byte)':');
                if (colon <= 0)
                    return headerLines >= 2 && classic >= 1;
                var name = TrimWhitespace(line[..colon]);
                if (!IsHeaderName(name))
                    return headerLines >= 2 && classic >= 1;

                headerLines++;
                if (IsClassicHeader(name))
                    classic++;
            }

            if (last)
                break;
        }

        return headerLines >= 2 && classic >= 1 && (seenBlank || headerLines >= 3);
    }

    private static ReadOnlySpan<byte> TrimWhitespace(ReadOnlySpan<byte> span)
    {
        var start = 0;
        var end = span.Length;
        while (start < end && IsAsciiWhitespace(span[start]))
            start++;
        while (end > start && IsAsciiWhitespace(span[end - 1]))
            end--;
        return span[start..end];
    }

    private static bool IsAsciiWhitespace(byte b) => b == (byte)' ' || b is >= 0x09 and <= 0x0D;

    private static bool IsHeaderName(ReadOnlySpan<byte> name)
    {
        if (name.Length == 0 || !IsAsciiLetterOrDigit(name[0]))
            return false;
        foreach (var b in name)
        {
            if (!IsAsciiLetterOrDigit(b) && b != (byte)'-' && b != (byte)'_')
                return false;
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(byte b)
        => b is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9');

    private static bool IsClassicHeader(ReadOnlySpan<byte> name)
    {
        foreach (var header in ClassicHeaders)
        {
            if (header.Length != name.Length)
                continue;
            var match = true;
            for (var i = 0; i < name.Length && match; i++)
            {
                var c = name[i];
                if (c is >= (byte)'A' and <= (byte)'Z')
                    c += 32;
                match = c == header[i];
            }

            if (match)
                return true;
        }

        return false;
    }

    private static bool HasMagic(ReadOnlySpan<byte> data, ReadOnlySpan<byte> magic)
        => data.Length >= magic.Length && data[..magic.Length].SequenceEqual(magic);

    private static bool StartsWithAscii(ReadOnlySpan<byte> data, string ascii)
    {
        if (data.Length < ascii.Length)
            return false;
        for (var i = 0; i < ascii.Length; i++)
        {
            if (data[i] != (byte)ascii[i])
                return false;
        }

        return true;
    }
}
