using EmlExtraction.Bench.Sweep;

namespace EmlExtraction.Tests;

// Pure parts of the bench large-file pass and sizing formula (ADR-015), linked from benches/EmlExtraction.Bench/Sweep.
public sealed class LargeFileMathTests
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(1_001)]
    [InlineData(1_002)]
    [InlineData(1_003)]
    [InlineData(1_004)]
    [InlineData(65_536)]
    [InlineData(1_048_577)]
    public void BuildMessageHasExactLengthAndOneAttachment(int size)
    {
        var bytes = LargeFileMath.BuildMessage(size);
        Assert.Equal(size, bytes.Length);
        var result = new EmlExtractor().Extract(bytes);
        var attachment = Assert.Single(result.Attachments);
        Assert.Equal("big.bin", attachment.FileName);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void BuildMessageRejectsTooSmall() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LargeFileMath.BuildMessage(10));

    [Theory]
    [InlineData(300, 100, 100, 2.0)]
    [InlineData(100, 300, 100, 0.0)]
    [InlineData(100, 0, 0, 0.0)]
    public void PeakGrowthIsDeltaOverInput(long peak, long pre, long input, double expected) =>
        Assert.Equal(expected, LargeFileMath.PeakGrowthPerInputByte(peak, pre, input), 9);

    [Fact]
    public void NBestIsHighestThroughputFirstOnTie()
    {
        Assert.Equal(3, LargeFileMath.NBest([10, 18, 25, 20]));
        Assert.Equal(1, LargeFileMath.NBest([10, 10]));
        Assert.Throws<ArgumentException>(() => LargeFileMath.NBest([]));
    }

    [Fact]
    public void SizingFormulas()
    {
        Assert.Equal(5, LargeFileMath.CoresNeeded(100, 30, 0.7));
        Assert.Equal(3, LargeFileMath.ServersNeeded(100, 40));
        Assert.Equal(1_000 + 4 * 100 * 3.0, LargeFileMath.RamPerServer(1_000, 4, 100, 3.0), 9);
    }
}
