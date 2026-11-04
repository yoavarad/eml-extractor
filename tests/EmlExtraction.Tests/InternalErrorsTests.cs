using EmlExtraction.Models;
using GoldenKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-026 gate helper and fault seam shared by GoldenKit score and probe.</summary>
[Collection("NonParallel")]
public sealed class InternalErrorsTests : IDisposable
{
    private static readonly EmlExtractionResult Clean = new(new Dictionary<string, string>(), null, null, [], ["parse_failed: x"]);

    public void Dispose() => InternalErrors.Injected = null;

    [Theory]
    [InlineData("internal_error: metadata: InvalidCastException", true)]
    [InlineData("internal_error: metadata.date: NullReferenceException", true)]
    [InlineData("internal_errors: x", false)]
    [InlineData("parse_failed: FormatException", false)]
    public void IsMatchesTheInternalErrorCode(string warning, bool expected)
        => Assert.Equal(expected, InternalErrors.Is(warning));

    [Fact]
    public void ApplyWithoutSeamReturnsResultUnchanged() => Assert.Same(Clean, InternalErrors.Apply(Clean));

    [Fact]
    public void ApplyWithSeamAppendsTheWarning()
    {
        InternalErrors.Injected = "internal_error: body: InvalidCastException";

        Assert.Equal(["parse_failed: x", "internal_error: body: InvalidCastException"], InternalErrors.Apply(Clean).Warnings);
    }

    [Fact]
    public void CheckedExtractorRejectsInternalError()
    {
        var result = new EmlExtractionResult(new Dictionary<string, string>(), null, null, [], ["internal_error: parse: InvalidCastException"]);

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => CheckedExtractor.Check(result));
    }
}
