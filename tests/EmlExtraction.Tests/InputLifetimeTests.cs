using System.Runtime.CompilerServices;
using System.Text;
using MimeKit;
using MimeKit.Tnef;

namespace EmlExtraction.Tests;

/// <summary>
/// T-opt-parse: the parser reads part content straight from the caller's buffer, so nothing in the result may still
/// point at that buffer once Extract returns (no lazy view escapes).
/// </summary>
public sealed class InputLifetimeTests
{
    private readonly IEmlExtractor _extractor = new CheckedExtractor();

    private static readonly string RealisticDir = FindDir();

    private static string FindDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EmlExtraction.slnx"))) d = d.Parent;
        return Path.Combine(d?.FullName ?? throw new InvalidOperationException("repo root not found"), "corpus", "realistic");
    }

    /// <summary>text + HTML alternative, a base64 attachment, a message/rfc822 part and a TNEF part (from the committed fixture).</summary>
    private static byte[] MultiPart()
    {
        var tnefSource = MimeMessage.Load(Path.Combine(RealisticDir, "tnef-docx.eml"));
        var tnef = tnefSource.BodyParts.OfType<TnefPart>().Single();

        var alternative = new MultipartAlternative
        {
            new TextPart("plain") { Text = "plain body שלום" },
            new TextPart("html") { Text = "<p>html body</p>" }
        };
        var payload = Enumerable.Range(0, 70_000).Select(i => (byte)(i * 7)).ToArray();
        var attachment = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream(payload)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = "data.bin"
        };
        var rfc822 = new MessagePart("rfc822")
        {
            Message = SampleFactory.Plain("inner subject", "inner body"),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "fwd.eml" }
        };

        var message = SampleFactory.Plain("outer subject", "unused");
        message.Body = new Multipart("mixed") { alternative, attachment, rfc822, tnef };
        return SampleFactory.ToBytes(message);
    }

    private static byte[] Mbox(byte[] eml) =>
        [.. Encoding.ASCII.GetBytes("From alice@example.com Tue Sep 15 07:00:00 2026\n"), .. eml];

    public static TheoryData<string> Inputs() => ["multipart", "mbox", "tnef-fixture"];

    private static byte[] Input(string name) => name switch
    {
        "multipart" => MultiPart(),
        "mbox" => Mbox(MultiPart()),
        "tnef-fixture" => File.ReadAllBytes(Path.Combine(RealisticDir, "tnef-docx.eml")),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    [Theory]
    [MemberData(nameof(Inputs))]
    public void OverwritingInputAfterExtractLeavesResultUnchanged(string name)
    {
        var input = Input(name);
        var expected = ResultProjection.Of(_extractor.Extract(input.ToArray()));

        var result = _extractor.Extract(input);
        Array.Fill(input, (byte)'X');

        Assert.Equal(expected, ResultProjection.Of(result));
        Assert.Empty(result.Warnings);
        Assert.NotEmpty(result.Attachments);
    }

    [Fact]
    public void ParsedContentIsAViewOverTheInput()
    {
        // Proves the parse is persistent: content is read from the caller's buffer when decoded, so Extract must copy it
        // out (and keep the stream open) before returning; the overwrite test above shows that it does.
        var input = Input("multipart");
        using var stream = EmlExtractor.Open(input);
        var message = EmlExtractor.Parse(stream, input);
        var part = message.BodyParts.OfType<MimePart>().Single(p => p.FileName == "data.bin");

        Assert.NotNull(part.Content);
        Array.Fill(input, (byte)'A');
        using var decoded = new MemoryStream();
        part.Content.DecodeTo(decoded);

        Assert.NotEqual(70_000, decoded.Length);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void OffsetSliceInputExtractsLikeWholeArray(string name)
    {
        var input = Input(name);
        var padded = new byte[input.Length + 10];
        input.CopyTo(padded, 5);
        var expected = ResultProjection.Of(_extractor.Extract(input));

        var result = _extractor.Extract(new ReadOnlyMemory<byte>(padded, 5, input.Length));
        Array.Fill(padded, (byte)'X');

        Assert.Equal(expected, ResultProjection.Of(result));
    }

    [Fact]
    public void MultiPartResultHasEveryPart()
    {
        var result = _extractor.Extract(MultiPart());
        Assert.Equal("<p>html body</p>", result.MainContent);
        Assert.Contains(result.Attachments, a => a.FileName == "data.bin" && a.SizeInBytes == 70_000);
        Assert.Contains(result.Attachments, a => a.FileName == "fwd.eml");
        Assert.Contains(result.Attachments, a => a.FullPath.Contains('/', StringComparison.Ordinal));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void TnefFixtureUnwrapsWithoutWarnings()
    {
        var result = _extractor.Extract(Input("tnef-fixture"));
        Assert.Contains(result.Attachments, a => a.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) && a.SizeInBytes > 0);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void MboxPathParsesEveryPart()
    {
        var eml = MultiPart();
        var plain = ResultProjection.Of(_extractor.Extract(eml));
        var result = _extractor.Extract(Mbox(eml));
        Assert.Equal(plain, ResultProjection.Of(result));
        Assert.Empty(result.Warnings);
        Assert.Contains(result.Attachments, a => a.FileName == "data.bin" && a.SizeInBytes == 70_000);
        Assert.Contains(result.Attachments, a => a.FileName == "fwd.eml");
    }

    [Fact]
    public void ResultDoesNotKeepInputAlive()
    {
        var (result, input) = ExtractAndForget(_extractor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(input.IsAlive);
        Assert.NotEmpty(result.Attachments);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (EmlExtractionResult Result, WeakReference Input) ExtractAndForget(IEmlExtractor extractor)
    {
        var bytes = MultiPart();
        return (extractor.Extract(bytes), new WeakReference(bytes));
    }
}
