using System;
using System.Text;

namespace EmlExtraction.Bench.Sweep;

/// <summary>Pure parts of the large-file pass and the sizing formula (ADR-015). Linked into the test project.</summary>
internal static class LargeFileMath
{
    private const int LineChars = 76;
    private const string Head =
        "From: load@example.com\r\nTo: sink@example.com\r\nSubject: large\r\nMIME-Version: 1.0\r\n" +
        "Content-Type: multipart/mixed; boundary=\"b\"\r\n\r\n--b\r\nContent-Type: text/plain\r\n\r\nbody\r\n" +
        "--b\r\nContent-Type: application/octet-stream; name=\"big.bin\"\r\nContent-Transfer-Encoding: base64\r\n" +
        "Content-Disposition: attachment; filename=\"big.bin\"\r\n\r\n";
    private const string Tail = "--b--\r\n";
    private const string PadPrefix = "X-Pad: ";

    /// <summary>Smallest size <see cref="BuildMessage"/> can hit.</summary>
    public static int MinMessageBytes => Head.Length + Tail.Length + PadPrefix.Length + 2 + 4 + 2;

    /// <summary>
    /// An EML of exactly <paramref name="totalBytes"/> bytes: small text part plus one base64 attachment; an
    /// <c>X-Pad</c> header absorbs the remainder so the length is exact.
    /// </summary>
    public static byte[] BuildMessage(long totalBytes)
    {
        if (totalBytes < MinMessageBytes || totalBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(totalBytes));
        var avail = (int)totalBytes - Head.Length - Tail.Length - (PadPrefix.Length + 2);
        // Largest multiple-of-4 char count n with n + 2 * ceil(n / 76) <= avail.
        var n = avail / (LineChars + 2) * LineChars;
        var rest = avail - n / LineChars * (LineChars + 2);
        if (rest > 2)
            n += Math.Min(rest - 2, LineChars - 1) / 4 * 4;
        n = n / 4 * 4;
        while (n + 2 * ((n + LineChars - 1) / LineChars) > avail)
            n -= 4;
        var bodyBytes = n + 2 * ((n + LineChars - 1) / LineChars);
        var pad = avail - bodyBytes;

        var buf = new byte[(int)totalBytes];
        var o = 0;
        o += Encoding.ASCII.GetBytes(PadPrefix, buf.AsSpan(o));
        buf.AsSpan(o, pad).Fill((byte)'a');
        o += pad;
        buf[o++] = (byte)'\r';
        buf[o++] = (byte)'\n';
        o += Encoding.ASCII.GetBytes(Head, buf.AsSpan(o));
        for (var done = 0; done < n;)
        {
            var len = Math.Min(LineChars, n - done);
            buf.AsSpan(o, len).Fill((byte)'A');
            o += len;
            done += len;
            buf[o++] = (byte)'\r';
            buf[o++] = (byte)'\n';
        }

        o += Encoding.ASCII.GetBytes(Tail, buf.AsSpan(o));
        return buf;
    }

    /// <summary>M = (peak - pre-run working set) / input bytes; never negative.</summary>
    public static double PeakGrowthPerInputByte(long peakWorkingSet, long preRunWorkingSet, long inputBytes) =>
        inputBytes <= 0 ? 0 : Math.Max(0, peakWorkingSet - preRunWorkingSet) / (double)inputBytes;

    /// <summary>The DOP n (1-based) of the highest throughput; the lowest n wins a tie.</summary>
    public static int NBest(IReadOnlyList<double> throughputByDop)
    {
        if (throughputByDop.Count == 0)
            throw new ArgumentException("No points.", nameof(throughputByDop));
        var best = 0;
        for (var i = 1; i < throughputByDop.Count; i++)
        {
            if (throughputByDop[i] > throughputByDop[best])
                best = i;
        }

        return best + 1;
    }

    /// <summary>cores_needed = ceil(R / (T(1) x E(n))).</summary>
    public static long CoresNeeded(double targetRate, double t1, double efficiency) =>
        (long)Math.Ceiling(targetRate / (t1 * efficiency));

    /// <summary>servers_needed = ceil(R / T(n_best)).</summary>
    public static long ServersNeeded(double targetRate, double tBest) => (long)Math.Ceiling(targetRate / tBest);

    /// <summary>ram_per_server = P(n_best) + n_best x max_input_bytes x M.</summary>
    public static double RamPerServer(double pBest, int nBest, long maxInputBytes, double m) =>
        pBest + (double)nBest * maxInputBytes * m;
}
