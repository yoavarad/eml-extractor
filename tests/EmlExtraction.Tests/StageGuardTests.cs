using System.Diagnostics.CodeAnalysis;
using MimeKit;

namespace EmlExtraction.Tests;

/// <summary>
/// ADR-023 stage wrappers, driven by the internal <see cref="EmlExtractor.StageFault"/> seam: an unexpected exception
/// in a stage throws <see cref="EmlExtractionException"/> naming that stage, with the injected exception as
/// <see cref="Exception.InnerException"/>. No partial result is returned (ports the #37 partial-result cases).
/// </summary>
public sealed class StageGuardTests
{
    private readonly EmlExtractor _extractor = new();

    [Theory]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void ByteOverloadWrapsStageFault(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertWrapped(stage, () => _extractor.Extract(bytes));
    }

    [Theory]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void RequestOverloadWrapsStageFault(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertWrapped(stage, () => _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes }));
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void MimeMessageOverloadWrapsStageFault(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertWrapped(stage, () => _extractor.Extract(MimeMessage.Load(new MemoryStream(bytes))));
    }

    [Fact]
    public void MetadataFaultThrowsInsteadOfDefaultSubjectDirection()
    {
        // #37 gap: a metadata fault used to leave SubjectDirection "ltr" on a result whose subject is RTL.
        var bytes = SampleFactory.ToBytes(Sample());
        Assert.Equal("rtl", _extractor.Extract(bytes).SubjectDirection);

        AssertWrapped("metadata", () => _extractor.Extract(bytes));
    }

    [Fact]
    public void BodySelectionFaultThrowsInsteadOfLeakingBodyAsAttachment()
    {
        // #37 gap: a body-stage fault left body null, so the attachments stage collected the body part as an attachment.
        var bytes = SampleFactory.ToBytes(Sample());
        Assert.DoesNotContain(_extractor.Extract(bytes).Attachments, a => a.FileName.EndsWith(".plain", StringComparison.Ordinal));

        AssertWrapped("body", () => _extractor.Extract(bytes));
    }

    [Fact]
    public void FirstFailingStageWins()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var ex = WithFault(
            s => s is "metadata" or "attachments" ? new InvalidCastException(s) : null,
            () => Assert.Throws<EmlExtractionException>(() => _extractor.Extract(bytes)));

        Assert.Equal("metadata", ex.Stage);
        Assert.Equal("metadata", ex.InnerException!.Message);
    }

    [Fact]
    public void ParseContentExceptionStaysParseFailed()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var result = WithFault(
            s => s == "parse" ? new ArgumentException("bad") : null,
            () => _extractor.Extract(bytes));

        Assert.StartsWith("parse_failed: ArgumentException", Assert.Single(result.Warnings), StringComparison.Ordinal);
        Assert.Null(result.MainContent);
        Assert.Empty(result.Attachments);
    }

    [Theory]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "The test injects a runtime-reserved OutOfMemoryException to prove it is not wrapped.")]
    public void OutOfMemoryIsNotWrapped(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var oom = new OutOfMemoryException("oom");
        var thrown = WithFault(
            s => s == stage ? oom : null,
            () => Assert.Throws<OutOfMemoryException>(() => _extractor.Extract(bytes)));
        Assert.Same(oom, thrown);
    }

    [Fact]
    public void EmlExtractionExceptionIsNotWrappedAgain()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var inner = new EmlExtractionException("metadata", new InvalidCastException("x"));
        var thrown = WithFault(
            s => s == "body" ? inner : null,
            () => Assert.Throws<EmlExtractionException>(() => _extractor.Extract(bytes)));
        Assert.Same(inner, thrown);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DetectorWrapsFaultWithStageDetect(bool isEml)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var detector = new EmlDetector();
        var boom = new InvalidCastException("boom");
        var ex = WithFault(
            s => s == "detect" ? boom : null,
            () => Assert.Throws<EmlExtractionException>(() =>
            {
                if (isEml)
                    detector.IsEml(bytes);
                else
                    detector.Detect(bytes);
            }));

        Assert.Equal("detect", ex.Stage);
        Assert.Same(boom, ex.InnerException);
    }

    [Fact]
    public void NoFaultReturnsCleanResult()
    {
        var result = _extractor.Extract(Sample());
        Assert.Empty(result.Warnings);
        Assert.NotNull(result.MainContent);
        Assert.NotEmpty(result.Attachments);
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

    private static void AssertWrapped(string stage, Func<EmlExtractionResult> extract)
    {
        var boom = new InvalidCastException("boom");
        var ex = WithFault(
            s => s == stage ? boom : null,
            () => Assert.Throws<EmlExtractionException>(() => extract()));

        Assert.Equal(stage, ex.Stage);
        Assert.Same(boom, ex.InnerException);
    }

    private static T WithFault<T>(Func<string, Exception?> fault, Func<T> run)
    {
        EmlExtractor.StageFault = s =>
        {
            if (fault(s) is { } ex)
                throw ex;
        };
        try
        {
            return run();
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }
    }
}
