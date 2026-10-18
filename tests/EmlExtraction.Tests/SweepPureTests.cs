using System.Text;
using EmlExtraction.Bench.Sweep;

namespace EmlExtraction.Tests;

// Pure parts of the bench DOP sweep (ADR-015), linked from benches/EmlExtraction.Bench/Sweep.
public sealed class SweepPureTests
{
    [Theory]
    [InlineData(100, 1, 100, 1.0)]
    [InlineData(180, 2, 100, 0.9)]
    [InlineData(400, 8, 100, 0.5)]
    [InlineData(50, 4, 0, 0.0)]
    public void EfficiencyIsTnOverNTimesT1(double tn, int n, double t1, double expected) =>
        Assert.Equal(expected, SweepMath.Efficiency(tn, n, t1), 9);

    [Fact]
    public void EfficiencyRejectsZeroDop() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SweepMath.Efficiency(1, 0, 1));

    [Fact]
    public void EfficiencyIsNullWithoutDop1Baseline()
    {
        Assert.Null(SweepMath.EfficiencyOrNull(400, 8, null));
        Assert.Equal(0.5, SweepMath.EfficiencyOrNull(400, 8, 100)!.Value, 9);
    }

    [Theory]
    [InlineData(50, 5.0)]
    [InlineData(95, 10.0)]
    [InlineData(100, 10.0)]
    [InlineData(10, 1.0)]
    [InlineData(11, 2.0)]
    public void PercentileIsNearestRank(double percent, double expected)
    {
        double[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(expected, SweepMath.Percentile(sorted, percent));
    }

    [Fact]
    public void PercentileOfSingleValue() => Assert.Equal(7.0, SweepMath.Percentile([7.0], 95));

    [Fact]
    public void PercentileRejectsEmptyAndOutOfRange()
    {
        Assert.Throws<ArgumentException>(() => SweepMath.Percentile([], 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => SweepMath.Percentile([1.0], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SweepMath.Percentile([1.0], 101));
    }

    [Fact]
    public void AllocPerInputByte()
    {
        Assert.Equal(4.0, SweepMath.AllocPerInputByte(400, 100));
        Assert.Equal(0.0, SweepMath.AllocPerInputByte(400, 0));
    }

    private const string LinuxX64 = """
        processor	: 0
        vendor_id	: GenuineIntel
        model name	: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz
        physical id	: 0
        core id		: 0

        processor	: 1
        model name	: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz
        physical id	: 0
        core id		: 0

        processor	: 2
        model name	: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz
        physical id	: 0
        core id		: 1

        processor	: 3
        model name	: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz
        physical id	: 1
        core id		: 0
        """;

    // ARM Linux /proc/cpuinfo has no "model name" or "core id".
    private const string LinuxArm = """
        processor	: 0
        BogoMIPS	: 50.00
        CPU implementer	: 0x41
        """;

    [Fact]
    public void ProcCpuinfoModelName() =>
        Assert.Equal("Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz", CpuInfoParser.ParseProcCpuinfoModel(LinuxX64));

    [Fact]
    public void ProcCpuinfoPhysicalCoresCountsDistinctSocketCorePairs() =>
        Assert.Equal(3, CpuInfoParser.ParseProcCpuinfoPhysicalCores(LinuxX64));

    [Fact]
    public void ProcCpuinfoWithoutModelOrCoreIdIsUnreadable()
    {
        Assert.Null(CpuInfoParser.ParseProcCpuinfoModel(LinuxArm));
        Assert.Null(CpuInfoParser.ParseProcCpuinfoPhysicalCores(LinuxArm));
    }

    [Fact]
    public void SysctlBrandString() =>
        Assert.Equal("Apple M1 Pro", CpuInfoParser.ParseSysctlValue("Apple M1 Pro\n"));

    [Theory]
    [InlineData("12th Gen Intel(R) Core(TM) i7-12700H   ", "12th Gen Intel(R) Core(TM) i7-12700H")]
    [InlineData("  AMD Ryzen 9  7950X 16-Core Processor  ", "AMD Ryzen 9 7950X 16-Core Processor")]
    public void WindowsProcessorNameStringIsNormalized(string raw, string expected) =>
        Assert.Equal(expected, CpuInfoParser.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown processor")]
    [InlineData("unknown")]
    public void PlaceholderCpuStringsAreUnreadable(string? raw) => Assert.Null(CpuInfoParser.Normalize(raw));

    [Fact]
    public void MboxSplitsOnFromLinesAfterBlankLine()
    {
        var mbox = Encoding.ASCII.GetBytes(
            "From a@x Mon Jan  1 00:00:00 2024\nSubject: one\n\nfirst line\nFrom here on, body text.\n\n" +
            "From b@x Mon Jan  1 00:00:01 2024\r\nSubject: two\r\n\r\nbody\r\n");

        var messages = MboxSplitter.Split(mbox).Select(Encoding.ASCII.GetString).ToArray();

        Assert.Equal(2, messages.Length);
        Assert.StartsWith("Subject: one\n", messages[0]);
        Assert.Contains("From here on, body text.", messages[0]);
        Assert.Equal("Subject: two\r\n\r\nbody\r\n", messages[1]);
    }

    [Fact]
    public void NonMboxIsOneMessage()
    {
        var eml = Encoding.ASCII.GetBytes("Subject: x\r\n\r\nbody");
        Assert.False(MboxSplitter.IsMbox("a.eml", eml));
        Assert.Same(eml, Assert.Single(MboxSplitter.Split(eml)));
    }

    [Fact]
    public void MboxDetectedByExtensionOrFromPrefix()
    {
        Assert.True(MboxSplitter.IsMbox("list.mbox", "Subject: x"u8));
        Assert.True(MboxSplitter.IsMbox("archive", "From a@x Mon\n"u8));
    }
}
