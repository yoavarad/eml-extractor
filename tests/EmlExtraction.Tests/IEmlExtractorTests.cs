using EmlExtraction.Abstractions;
using EmlExtraction.Exceptions;
using EmlExtraction.Logics;

namespace EmlExtraction.Tests;

/// <summary>
/// The documented <see cref="IEmlExtractor"/> contract (ADR-026): a result is always returned, except for misuse,
/// input over the size limit, and <see cref="OutOfMemoryException"/>. Extraction never throws <see cref="EmlExtractionException"/>.
/// </summary>
public sealed class IEmlExtractorTests
{
    [Fact]
    public void UnreadableInputReturnsResultInsteadOfThrowing()
    {
        IEmlExtractor extractor = new EmlExtractor();

        var result = extractor.Extract(new byte[] { 0x00, 0x01, 0x02 });

        Assert.NotEmpty(result.Warnings);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("internal_error", StringComparison.Ordinal));
    }

    [Fact]
    public void OversizedInputThrowsEmlInputTooLargeException()
    {
        var extractor = new EmlExtractor(maxInputBytes: 4);

        Assert.Throws<EmlInputTooLargeException>(() => extractor.Extract(new byte[5]));
    }
}
