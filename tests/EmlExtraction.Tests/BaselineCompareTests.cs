using EmlExtraction.Bench.Sweep;

namespace EmlExtraction.Tests;

[CollectionDefinition("NonParallel", DisableParallelization = true)]
public sealed class NonParallelCollectionDefinition;

// Comparability check and warn-only regression rule (task 50), linked from benches/EmlExtraction.Bench/Sweep.
[Collection("NonParallel")]
public sealed class BaselineCompareTests
{
    private const int Cores = 4;

    private static SweepPoint Point(int dop, double t, double a = 10, long p = 1000) =>
        new(dop, t, 1, 2, p, a, 1, 100, 1, 1);

    private static SweepReport Report(
        string cpu = "CPU X",
        int cores = Cores,
        string runtime = ".NET 10.0.12",
        string[]? modes = null,
        double t1 = 100,
        double tn = 300,
        double a = 10,
        long p = 1000) =>
        new(
            1,
            new HardwareBlock(cpu, 2, cores, 1L << 30, "OS", "x64", runtime, "net10.0", "2026-10-18T00:00:00Z", "abc"),
            new SweepParameters(1, 1, 1, 50, cores, "synthetic", ""),
            (modes ?? ["workstation", "server"]).Select(m => new GcRun(m,
            [
                new SetResult("synthetic", "measured", null, 5, 1000,
                    [Point(1, t1), Point(2, t1 * 2), Point(cores, tn, a, p)], []),
            ])).ToList());

    [Fact]
    public void IdenticalReportsHaveNoWarnings()
    {
        var r = BaselineCompare.Compare(Report(), Report());
        Assert.True(r.Comparable);
        Assert.Empty(r.Warnings);
        Assert.Equal(8, r.MetricsChecked);
    }

    [Theory]
    [InlineData("cpu", "cpuModel")]
    [InlineData("cores", "logicalCores")]
    [InlineData("runtime", "runtime")]
    [InlineData("gc", "gcMode")]
    public void DifferentEnvironmentIsNotComparable(string what, string field)
    {
        var current = what switch
        {
            "cpu" => Report(cpu: "CPU Y"),
            "cores" => Report(cores: 8),
            "runtime" => Report(runtime: ".NET 9.0.1"),
            _ => Report(modes: ["workstation", "concurrent"]),
        };
        var r = BaselineCompare.Compare(Report(), current);
        Assert.False(r.Comparable);
        Assert.Equal(field, r.NotComparableField);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void RuntimePatchDifferenceIsStillComparable() =>
        Assert.True(BaselineCompare.Compare(Report(), Report(runtime: ".NET 10.0.99")).Comparable);

    [Fact]
    public void TwentyFivePercentSlowdownWarns()
    {
        var r = BaselineCompare.Compare(Report(), Report(t1: 75, tn: 225));
        Assert.True(r.Comparable);
        Assert.Contains(r.Warnings, w => w.Contains("T(1) dropped 25.0%"));
        Assert.Contains(r.Warnings, w => w.Contains($"T({Cores}) dropped 25.0%"));
    }

    [Fact]
    public void AllocAndPeakRiseWarns()
    {
        var r = BaselineCompare.Compare(Report(), Report(a: 13, p: 1300));
        Assert.Contains(r.Warnings, w => w.Contains(" A rose 30.0%"));
        Assert.Contains(r.Warnings, w => w.Contains($"P({Cores}) rose 30.0%"));
    }

    [Fact]
    public void ChangesWithinThresholdAndImprovementsDoNotWarn()
    {
        Assert.Empty(BaselineCompare.Compare(Report(), Report(t1: 85, tn: 250, a: 11, p: 1100)).Warnings);
        Assert.Empty(BaselineCompare.Compare(Report(), Report(t1: 200, tn: 900, a: 1, p: 10)).Warnings);
    }

    [Fact]
    public void ExactlyAtThresholdDoesNotWarn() =>
        Assert.Empty(BaselineCompare.Compare(Report(), Report(t1: 80, a: 12)).Warnings);

    [Fact]
    public void SkippedSetIsIgnored()
    {
        var current = Report() with
        {
            Runs = [new GcRun("workstation", [new SetResult("synthetic", "skipped", "x", 0, 0, [], [])]),
                    new GcRun("server", [new SetResult("synthetic", "skipped", "x", 0, 0, [], [])])],
        };
        var r = BaselineCompare.Compare(Report(), current);
        Assert.True(r.Comparable);
        Assert.Equal(0, r.MetricsChecked);
    }

    [Theory]
    [InlineData(".NET 10.0.12", "10.0")]
    [InlineData(".NET 8.0.1", "8.0")]
    [InlineData("weird", "weird")]
    public void RuntimeMajorMinorParses(string input, string expected) =>
        Assert.Equal(expected, BaselineCompare.RuntimeMajorMinor(input));

    [Fact]
    public void CompareCommandExitsZeroOnSlowdownAndOnIncomparable()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string Save(string name, SweepReport r)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(r, SweepReport.Json));
                return path;
            }

            var b = Save("b.json", Report());
            var slow = Save("slow.json", Report(t1: 75, tn: 225));
            var other = Save("other.json", Report(cpu: "Other"));
            var stdout = Console.Out;
            var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                Assert.Equal(0, CompareCommand.Run(["--baseline", b, "--current", slow]));
                Assert.Equal(0, CompareCommand.Run(["--baseline", b, "--current", other]));
            }
            finally
            {
                Console.SetOut(stdout);
            }

            Assert.Contains("T(1) dropped", sw.ToString());
            Assert.Contains("baseline not comparable: cpuModel", sw.ToString());
            Assert.Equal(2, CompareCommand.Run(["--baseline", b]));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
