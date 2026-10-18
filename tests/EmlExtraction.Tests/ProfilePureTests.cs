using EmlExtraction.Bench.Profile;

namespace EmlExtraction.Tests;

// Pure parts of the per-stage profiling harness (ADR-020), linked from benches/EmlExtraction.Bench/Profile.
public sealed class ProfilePureTests
{
    [Theory]
    [InlineData(1000, 1000, 1.0)]
    [InlineData(3900, 1000, 3.9)]
    [InlineData(0, 10, 0.0)]
    public void CopyFactorIsAllocatedOverDecoded(long allocated, long decoded, double expected) =>
        Assert.Equal(expected, ProfileMath.CopyFactor(allocated, decoded)!.Value, 9);

    [Fact]
    public void CopyFactorIsNullWithoutDecodedBytes() => Assert.Null(ProfileMath.CopyFactor(500, 0));

    [Fact]
    public void CopyFactorRejectsNegative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileMath.CopyFactor(-1, 1));

    [Fact]
    public void PicksMostHeadersMostAttachmentsAndLargestTnef()
    {
        SampleFeatures[] samples =
        [
            new("a", 100, 40, 0, false),
            new("b", 200, 10, 7, false),
            new("c", 300, 10, 1, true),
            new("d", 900, 10, 0, true)
        ];

        Assert.Equal(["a", "b", "d"], ProfileMath.PickSamples(samples));
    }

    [Fact]
    public void PicksAreDistinctAndSkipMissingShapes()
    {
        SampleFeatures[] samples = [new("only", 10, 5, 0, false)];

        Assert.Equal(["only"], ProfileMath.PickSamples(samples));
    }

    [Fact]
    public void TiesGoToLargerSampleThenOrdinalName()
    {
        SampleFeatures[] samples =
        [
            new("z", 100, 9, 2, false),
            new("y", 500, 9, 2, false),
            new("x", 500, 9, 2, false)
        ];

        Assert.Equal(["x"], ProfileMath.PickSamples(samples));
    }

    [Fact]
    public void PicksNothingFromEmptyCorpus() => Assert.Empty(ProfileMath.PickSamples([]));
}
