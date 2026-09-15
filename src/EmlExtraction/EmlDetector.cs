using System.Text;

namespace EmlExtraction;

public sealed class EmlDetector : IEmlDetector
{
    private const int MinLength = 20;
    private const int SniffLimit = 16 * 1024;

    private static readonly HashSet<string> ClassicHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "from", "date", "received", "mime-version", "message-id", "subject",
        "return-path", "to", "delivered-to", "cc", "sender", "reply-to",
        "in-reply-to", "references", "content-type"
    };

    public bool IsEml(ReadOnlySpan<byte> data)
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

    private static bool LooksLikeHeaderBlock(ReadOnlySpan<byte> window)
    {
        var lines = Encoding.ASCII.GetString(window).Split(["\r\n", "\n"], StringSplitOptions.None);
        var headerLines = 0;
        var classic = 0;
        var seenBlank = false;

        foreach (var raw in lines)
        {
            if (raw.Length == 0)
            {
                seenBlank = headerLines > 0;
                break;
            }

            if (raw[0] is ' ' or '\t')
            {
                if (headerLines == 0)
                    return false;
                headerLines++;
                continue;
            }

            var colon = raw.IndexOf(':');
            if (colon <= 0 || !IsHeaderName(raw[..colon].Trim()))
                return headerLines >= 2 && classic >= 1;

            headerLines++;
            if (ClassicHeaders.Contains(raw[..colon].Trim()))
                classic++;
        }

        return headerLines >= 2 && classic >= 1 && (seenBlank || headerLines >= 3);
    }

    private static bool IsHeaderName(string name)
    {
        if (name.Length == 0 || !(char.IsLetter(name[0]) || char.IsDigit(name[0])))
            return false;
        return name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
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
