using EmlExtraction.Abstractions;
using EmlExtraction.Exceptions;
using EmlExtraction.Logics;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>
/// <see cref="EmlExtractor"/> that fails the calling test unless every warning is a content warning (ADR-023). A library
/// bug throws <see cref="EmlExtractionException"/>, which fails the test on its own.
/// </summary>
internal sealed class CheckedExtractor : IEmlExtractor
{
    private static readonly string[] ContentCodes =
        ["parse_failed", "body_decode_failed", "part_decode_failed", "rfc822_extract_failed", "tnef_extract_failed", "metadata_field_failed"];

    private readonly EmlExtractor _inner = new();

    public EmlExtractionResult Extract(ReadOnlyMemory<byte> eml) => Check(_inner.Extract(eml));

    public EmlExtractionResult Extract(Stream eml) => Check(_inner.Extract(eml));

    public EmlExtractionResult Extract(EmlExtractionRequest request) => Check(_inner.Extract(request));

    public static EmlExtractionResult Check(EmlExtractionResult result)
    {
        // ADR-026: an internal_error warning is a library bug; seed-golden, realistic-sample and internal-corpus fail on it.
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("internal_error:", StringComparison.Ordinal));
        Assert.All(result.Warnings, w => Assert.Contains(w.Split(':', 2)[0], ContentCodes));
        return result;
    }
}
