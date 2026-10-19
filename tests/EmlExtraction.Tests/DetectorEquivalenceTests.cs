using System.Text;
using EmlExtraction;

namespace EmlExtraction.Tests;

/// <summary>Pins the detector verdict against a frozen copy of the pre-#132 string-split implementation.</summary>
public sealed class DetectorEquivalenceTests
{
    private static readonly EmlDetector Detector = new();

    public static TheoryData<string, string> EdgeCases() => new()
    {
        { "lf-only", "From: a@b.c\nTo: d@e.f\nSubject: hi\n\nbody text here" },
        { "crlf", "From: a@b.c\r\nTo: d@e.f\r\nSubject: hi\r\n\r\nbody text here" },
        { "mixed", "From: a@b.c\r\nTo: d@e.f\nSubject: hi\r\n\nbody text here" },
        { "continuation-first", " folded: x\nFrom: a@b.c\nTo: d@e.f\n\nbody text here ok" },
        { "tab-continuation", "From: a@b.c\nTo: d@e.f\n\tmore\nSubject: x\n\nbody" },
        { "colon-at-zero", ":From a@b.c\nTo: d@e.f\nSubject: hi\n\nbody text here" },
        { "two-headers-no-blank", "From: a@b.c\nTo: d@e.f\nnot a header line here" },
        { "three-headers-no-blank", "From: a@b.c\nTo: d@e.f\nSubject: x\nno colon line" },
        { "no-classic", "X-One: a\nX-Two: b\nX-Three: c\n\nbody text here ok" },
        { "blank-after-two", "X-One: a\nFrom: b\n\nbody text here ok now" },
        { "cut-mid-line", "From: a@b.c\nTo: d@e.f\nSubject: hi\nX-Long: abcdefghij" },
        { "cut-mid-name", "From: a@b.c\nTo: d@e.f\nSubj" },
        { "trailing-lf", "From: a@b.c\nTo: d@e.f\n" },
        { "trailing-crlf", "From: a@b.c\r\nTo: d@e.f\r\n" },
        { "trailing-cr-only", "From: a@b.c\nTo: d@e.f\nSubject: x\r" },
        { "lone-cr-line", "From: a@b.c\nTo: d@e.f\r\rSubject: x\n\nb" },
        { "space-before-colon", "From : a@b.c\nTo\t: d@e.f\nSubject: hi\n\nbody text" },
        { "ctl-trim", "\vFrom: a@b.c\nTo\f: d@e.f\nSubject: hi\n\nbody text" },
        { "upper-case", "FROM: a@b.c\nTO: d@e.f\nSUBJECT: hi\n\nbody text here" },
        { "digit-name", "1abc: a\n2def: b\nFrom: c\n\nbody text here" },
        { "underscore-lead", "_x: a\nFrom: b\nTo: c\n\nbody text here" },
        { "bad-name-char", "Fr.om: a@b.c\nTo: d@e.f\nSubject: hi\n\nbody text" },
        { "mbox-from", "From alice@example.com Tue Sep 15 07:00:00 2026\nFrom: a@b.c\nTo: d@e.f\n\nbody" },
        { "mbox-from-no-nl", "From alice@example.com Tue Sep 15 07:00:00 2026 and more text" },
        { "mbox-from-nl-last", "From alice@example.com Tue Sep 15 07:00:00 2026\n" },
        { "nonascii-name", "Frö: a\nTo: b\nSubject: hi\n\nbody text here" },
        { "nonascii-value", "From: ä\nTo: ö\nSubject: ü\n\nbody text here" },
        { "classic-after-nonheader", "X-A: 1\nX-B: 2\nbad line\nFrom: a" },
        { "no-colon-first", "hello world this is text\nFrom: a\nTo: b\n\n" },
        { "empty-first-line", "\nFrom: a@b.c\nTo: d@e.f\nSubject: hi\n\nbody" },
        { "ctl-1c-name", "\u001cFrom: a\nTo\u001f: b\nSubject: hi\n\nbody text here ok" },
        { "nbsp-nel-name", " From: a\nTo\u0085: b\nSubject: hi\n\nbody text here ok" },
        { "nul-bytes", "From: a\0b\nTo: c\nSubject: x\n\nbody text here ok" },
    };

    [Theory]
    [MemberData(nameof(EdgeCases))]
    public void EdgeCaseMatchesLegacy(string name, string text)
    {
        foreach (var bytes in Variants(Latin1(text)))
            Assert.True(Legacy.Sniff(bytes) == Detector.IsEml(bytes), name);
    }

    [Fact]
    public void LargeWindowBoundariesMatchLegacy()
    {
        const string head = "From: a@b.c\nTo: d@e.f\nSubject: hi\n";
        foreach (var total in new[] { 16383, 16384, 16385, 20000 })
        {
            var sb = new StringBuilder(head);
            while (sb.Length < total)
                sb.Append("X-Pad: ").Append('p', 40).Append('\n');
            var bytes = Latin1(sb.ToString(0, total));
            Assert.Equal(Legacy.Sniff(bytes), Detector.IsEml(bytes));
            bytes = Latin1(sb.ToString(0, total).Replace("\n", "\r\n"));
            Assert.Equal(Legacy.Sniff(bytes), Detector.IsEml(bytes));
        }
    }

    [Fact]
    public void CorpusAndGoldenInputsMatchLegacy()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "corpus", "golden")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var files = Directory.EnumerateFiles(Path.Combine(dir.FullName, "corpus"), "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".msg", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(files);
        foreach (var f in files)
        {
            var bytes = File.ReadAllBytes(f);
            Assert.True(Legacy.Sniff(bytes) == Detector.IsEml(bytes), f);
            // Truncations exercise window cuts.
            foreach (var cut in new[] { 20, 64, 200, 1000, 16384 })
            {
                if (cut < bytes.Length)
                    Assert.True(Legacy.Sniff(bytes[..cut]) == Detector.IsEml(bytes[..cut]), f + "@" + cut);
            }
        }
    }

    [Fact]
    public void RandomMutationsMatchLegacy()
    {
        var rng = new Random(132);
        var seed = Latin1("From: a@b.c\r\nTo: d@e.f\nReceived: x\r\n y\r\nSubject: hi\r\n\r\nbody\r\n");
        var alphabet = new byte[] { (byte)'\n', (byte)'\r', (byte)':', (byte)' ', (byte)'\t', (byte)'X', (byte)'-', 0x80, 0xFF, 0x1C, 0x1F, 0x85, 0xA0, 0, (byte)'a' };
        for (var i = 0; i < 20000; i++)
        {
            var bytes = (byte[])seed.Clone();
            for (var m = rng.Next(1, 4); m > 0; m--)
                bytes[rng.Next(bytes.Length)] = alphabet[rng.Next(alphabet.Length)];
            var len = rng.Next(0, 4) == 0 ? rng.Next(bytes.Length + 1) : bytes.Length;
            var slice = bytes.AsSpan(0, len).ToArray();
            Assert.True(Legacy.Sniff(slice) == Detector.IsEml(slice), Convert.ToHexString(slice));
        }
    }

    private static byte[] Latin1(string s) => Encoding.Latin1.GetBytes(s);

    private static IEnumerable<byte[]> Variants(byte[] b)
    {
        yield return b;
        yield return [0xEF, 0xBB, 0xBF, .. b];
    }

    /// <summary>Frozen copy of the pre-#132 EmlDetector.Sniff (string decode and Split).</summary>
    private static class Legacy
    {
        private static readonly HashSet<string> ClassicHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "from", "date", "received", "mime-version", "message-id", "subject",
            "return-path", "to", "delivered-to", "cc", "sender", "reply-to",
            "in-reply-to", "references", "content-type"
        };

        public static bool Sniff(ReadOnlySpan<byte> data)
        {
            if (data.Length < 20)
                return false;
            var span = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? data[3..] : data;
            if (NonEmail(span))
                return false;
            var window = span.Length > 16 * 1024 ? span[..(16 * 1024)] : span;
            if (window.Length >= 5 && "From "u8.SequenceEqual(window[..5]))
            {
                var newline = window.IndexOf((byte)'\n');
                if (newline <= 0 || newline + 1 >= window.Length)
                    return false;
                window = window[(newline + 1)..];
            }

            return LooksLikeHeaderBlock(window);
        }

        private static bool NonEmail(ReadOnlySpan<byte> span)
            => span.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 })
               || span.StartsWith("%PDF"u8)
               || span.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })
               || span.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })
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
    }
}
