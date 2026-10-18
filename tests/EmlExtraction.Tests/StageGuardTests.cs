using MimeKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-004 per-stage guards, driven by the internal <see cref="EmlExtractor.StageFault"/> seam.</summary>
public sealed class StageGuardTests
{
    private static readonly string[] FixedKeys = ["bcc", "cc", "from", "to", "xfields"];

    private readonly EmlExtractor _extractor = new();

    [Fact]
    public void MetadataFaultKeepsFixedKeysAndOtherStages()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var baseline = _extractor.Extract(bytes);

        var result = WithFault("metadata", () => _extractor.Extract(bytes));

        AssertFixedKeysOnly(result);
        Assert.Equal("", result.Metadata["xfields"]);
        Assert.Equal("rtl", baseline.SubjectDirection);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal(baseline.MainContent, result.MainContent);
        Assert.Equal(baseline.MainContentMimeType, result.MainContentMimeType);
        Assert.Equal(baseline.ContentDirection, result.ContentDirection);
        Assert.Equal(Names(baseline), Names(result));
        AssertOneInternalError(result, "metadata");
    }

    [Fact]
    public void BodyFaultNullsContentAndKeepsOtherStages()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var baseline = _extractor.Extract(bytes);

        var result = WithFault("body", () => _extractor.Extract(bytes));

        Assert.Null(result.MainContent);
        Assert.Null(result.MainContentMimeType);
        Assert.Equal("rtl", baseline.ContentDirection);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("ltr", result.ContentDirection);
        Assert.Equal(baseline.Metadata, result.Metadata);
        Assert.Equal(baseline.Metadata["xfields"], result.Metadata["xfields"]);
        Assert.Equal(Names(baseline), Names(result));
        AssertOneInternalError(result, "body");
    }

    [Fact]
    public void AttachmentsFaultDropsAttachmentsAndKeepsOtherStages()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var baseline = _extractor.Extract(bytes);
        Assert.NotEmpty(baseline.Attachments);

        var result = WithFault("attachments", () => _extractor.Extract(bytes));

        Assert.Empty(result.Attachments);
        Assert.Equal(baseline.Metadata, result.Metadata);
        Assert.Equal(baseline.Metadata["xfields"], result.Metadata["xfields"]);
        Assert.Equal(baseline.MainContent, result.MainContent);
        Assert.Equal(baseline.SubjectDirection, result.SubjectDirection);
        Assert.Equal(baseline.ContentDirection, result.ContentDirection);
        AssertOneInternalError(result, "attachments");
    }

    [Fact]
    public void UnlistedParseExceptionGivesResultNotThrow()
    {
        var bytes = SampleFactory.ToBytes(Sample());

        var result = WithFault("parse", () => _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes }));

        AssertFixedKeysOnly(result);
        Assert.Equal("", result.Metadata["xfields"]);
        Assert.Null(result.MainContent);
        Assert.Null(result.MainContentMimeType);
        Assert.Empty(result.Attachments);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("ltr", result.ContentDirection);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        AssertOneInternalError(result, "parse");
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void MimeMessageOverloadMatchesByteOverload(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var fromBytes = WithFault(stage, () => _extractor.Extract(bytes));
        var fromMessage = WithFault(stage, () => _extractor.Extract(MimeMessage.Load(new MemoryStream(bytes))));

        Assert.Equal(ResultProjection.Of(fromBytes), ResultProjection.Of(fromMessage));
        AssertOneInternalError(fromMessage, stage);
    }

    [Fact]
    public void EachFailedStageAddsOneWarningInStageOrder()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        EmlExtractor.StageFault = s =>
        {
            if (s is "metadata" or "attachments")
                throw new InvalidCastException(s);
        };
        EmlExtractionResult result;
        try
        {
            result = _extractor.Extract(bytes);
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }

        Assert.Equal(
            ["internal_error: metadata: InvalidCastException: metadata", "internal_error: attachments: InvalidCastException: attachments"],
            result.Warnings);
        Assert.NotNull(result.MainContent);
    }

    [Fact]
    public void NoFaultMeansNoInternalError()
    {
        var result = _extractor.Extract(Sample());
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("internal_error", StringComparison.Ordinal));
    }

    [Fact]
    public void NullMimeMessageStillThrows()
        => Assert.Throws<ArgumentNullException>(() => _extractor.Extract((MimeMessage)null!));

    private static MimeMessage Sample()
    {
        var message = SampleFactory.Plain("שלום עולם", "unused");
        message.Headers.Add("X-Trace", "abc");
        var builder = new BodyBuilder { TextBody = "שלום לכולם, זה גוף ההודעה" };
        builder.Attachments.Add("data.bin", [1, 2, 3, 4], new ContentType("application", "octet-stream"));
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static EmlExtractionResult WithFault(string stage, Func<EmlExtractionResult> extract)
    {
        EmlExtractor.StageFault = s =>
        {
            if (s == stage)
                throw new InvalidCastException("boom");
        };
        try
        {
            return extract();
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }
    }

    private static void AssertFixedKeysOnly(EmlExtractionResult result)
    {
        Assert.Equal(FixedKeys, result.Metadata.Keys.Order(StringComparer.Ordinal));
        Assert.All(result.Metadata.Values, v => Assert.Equal("", v));
    }

    private static void AssertOneInternalError(EmlExtractionResult result, string stage)
    {
        var errors = result.Warnings.Where(w => w.StartsWith("internal_error", StringComparison.Ordinal)).ToList();
        Assert.Equal($"internal_error: {stage}: InvalidCastException: boom", Assert.Single(errors));
    }

    private static IEnumerable<string> Names(EmlExtractionResult result) => result.Attachments.Select(a => a.Name);
}
