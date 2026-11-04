using System.Diagnostics.CodeAnalysis;
using EmlExtraction.Exceptions;
using EmlExtraction.Logics;
using EmlExtraction.Models;
using MimeKit;

namespace EmlExtraction.Tests;

/// <summary>
/// ADR-026 stage guards, driven by the internal <see cref="EmlExtractor.StageFault"/> seam: an unexpected exception
/// (library bug) in a stage returns a partial result with <c>internal_error: &lt;stage&gt;: &lt;Type&gt;</c>; the failed
/// stage gets its empty default and the other stages keep their output. Detection still throws
/// <see cref="EmlExtractionException"/> (stage detect).
/// </summary>
public sealed class StageGuardTests
{
    private readonly EmlExtractor _extractor = new();

    [Theory]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void ByteOverloadReturnsInternalError(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertInternalError(stage, () => _extractor.Extract(bytes));
    }

    [Theory]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void RequestOverloadReturnsInternalError(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertInternalError(stage, () => _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes }));
    }

    [Theory]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void StreamOverloadReturnsInternalError(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertInternalError(stage, () => _extractor.Extract(new MemoryStream(bytes)));
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void MimeMessageOverloadReturnsInternalError(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        AssertInternalError(stage, () => _extractor.Extract(MimeMessage.Load(new MemoryStream(bytes))));
    }

    [Fact]
    public void ParseBugGivesEmptyResultWithoutParseFailed()
    {
        var result = Faulted("parse");

        Assert.Equal("internal_error: parse: InvalidCastException", Assert.Single(result.Warnings));
        Assert.Equal(HeaderMapper.EmptyMetadata(), result.Metadata);
        Assert.All(result.Participants.Values, Assert.Empty);
        Assert.Equal(8, result.Participants.Count);
        Assert.Null(result.MainContent);
        Assert.Null(result.MainContentMimeType);
        Assert.Empty(result.Attachments);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("ltr", result.ContentDirection);
    }

    [Fact]
    public void MetadataBugKeepsBodyAndAttachments()
    {
        var result = Faulted("metadata");

        Assert.Equal(HeaderMapper.EmptyMetadata(), result.Metadata);
        Assert.All(result.Participants.Values, Assert.Empty);
        Assert.Equal(8, result.Participants.Count);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("שלום לכולם, זה גוף ההודעה", result.MainContent);
        Assert.Equal("rtl", result.ContentDirection);
        Assert.Equal("data.bin", Assert.Single(result.Attachments).FileName);
    }

    [Fact]
    public void BodyBugKeepsMetadataAndAttachments()
    {
        var result = Faulted("body");

        Assert.Equal("שלום עולם", result.Metadata["subject"]);
        Assert.Equal("alice@example.com", Assert.Single(result.Participants["from"]).Address);
        Assert.Null(result.MainContent);
        Assert.Null(result.MainContentMimeType);
        Assert.Equal("ltr", result.SubjectDirection);
        Assert.Equal("ltr", result.ContentDirection);
        // ADR-026 stage default: no body part is known, so the text part is collected as an attachment (owner to confirm).
        Assert.Equal(2, result.Attachments.Count);
        Assert.Contains(result.Attachments, a => a.FileName == "data.bin");
        Assert.Contains(result.Attachments, a => a.FileName.EndsWith(".plain", StringComparison.Ordinal));
    }

    [Fact]
    public void AttachmentsBugKeepsMetadataAndBody()
    {
        var result = Faulted("attachments");

        Assert.Equal("שלום עולם", result.Metadata["subject"]);
        Assert.Equal("rtl", result.SubjectDirection);
        Assert.Equal("שלום לכולם, זה גוף ההודעה", result.MainContent);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public void EveryFailingStageRecordsItsOwnWarningInOrder()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var result = WithFault(
            s => s is "metadata" or "attachments" ? new InvalidCastException(s) : null,
            () => _extractor.Extract(bytes));

        Assert.Equal(
            ["internal_error: metadata: InvalidCastException", "internal_error: attachments: InvalidCastException"],
            result.Warnings);
        Assert.NotNull(result.MainContent);
    }

    [Fact]
    public void WarningCarriesNoExceptionMessage()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var result = WithFault(
            s => s == "body" ? new InvalidCastException("SECRET-subject-text") : null,
            () => _extractor.Extract(bytes));

        Assert.DoesNotContain(result.Warnings, w => w.Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public void EmlExtractionExceptionInsideAStageIsAnInternalError()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var result = WithFault(
            s => s == "body" ? new EmlExtractionException("metadata", new InvalidCastException("x")) : null,
            () => _extractor.Extract(bytes));

        Assert.Equal("internal_error: body: EmlExtractionException", Assert.Single(result.Warnings));
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
    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "The test injects a runtime-reserved OutOfMemoryException to prove it propagates.")]
    public void OutOfMemoryPropagates(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        var oom = new OutOfMemoryException("oom");
        var thrown = WithFault(
            s => s == stage ? oom : null,
            () => Assert.Throws<OutOfMemoryException>(() => _extractor.Extract(bytes)));
        Assert.Same(oom, thrown);
    }

    [Fact]
    public void InputTooLargeOnStreamPathStillThrows()
    {
        var bytes = SampleFactory.ToBytes(Sample());
        Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(16).Extract(new MemoryStream(bytes)));
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

    private EmlExtractionResult Faulted(string stage)
    {
        var bytes = SampleFactory.ToBytes(Sample());
        return WithFault(s => s == stage ? new InvalidCastException("boom") : null, () => _extractor.Extract(bytes));
    }

    private static MimeMessage Sample()
    {
        var message = SampleFactory.Plain("שלום עולם", "unused");
        message.Headers.Add("X-Trace", "abc");
        var builder = new BodyBuilder { TextBody = "שלום לכולם, זה גוף ההודעה" };
        builder.Attachments.Add("data.bin", [1, 2, 3, 4], new ContentType("application", "octet-stream"));
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static void AssertInternalError(string stage, Func<EmlExtractionResult> extract)
    {
        var result = WithFault(s => s == stage ? new InvalidCastException("boom") : null, extract);
        Assert.Equal($"internal_error: {stage}: InvalidCastException", Assert.Single(result.Warnings));
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
