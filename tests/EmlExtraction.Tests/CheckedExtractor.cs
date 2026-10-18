namespace EmlExtraction.Tests;

/// <summary><see cref="EmlExtractor"/> that fails the calling test on any <c>internal_error</c> warning (ADR-004).</summary>
internal sealed class CheckedExtractor : IEmlExtractor
{
    private readonly EmlExtractor _inner = new();

    public EmlExtractionResult Extract(ReadOnlyMemory<byte> eml) => Check(_inner.Extract(eml));

    public EmlExtractionResult Extract(EmlExtractionRequest request) => Check(_inner.Extract(request));

    public static EmlExtractionResult Check(EmlExtractionResult result)
    {
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("internal_error", StringComparison.Ordinal));
        return result;
    }
}
