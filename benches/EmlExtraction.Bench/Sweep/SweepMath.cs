namespace EmlExtraction.Bench.Sweep;

/// <summary>Pure sweep arithmetic (ADR-015). Linked into the test project.</summary>
internal static class SweepMath
{
    /// <summary>Scaling efficiency E(n) = T(n) / (n x T(1)).</summary>
    public static double Efficiency(double throughput, int dop, double throughputAtOne)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dop, 1);
        return throughputAtOne <= 0 ? 0 : throughput / (dop * throughputAtOne);
    }

    /// <summary>E(n), or null when there is no DOP 1 measurement to compare against.</summary>
    public static double? EfficiencyOrNull(double throughput, int dop, double? throughputAtOne) =>
        throughputAtOne is null ? null : Efficiency(throughput, dop, throughputAtOne.Value);

    /// <summary>Nearest-rank percentile of an ascending-sorted sample; <paramref name="percent"/> in (0, 100].</summary>
    public static double Percentile(IReadOnlyList<double> sorted, double percent)
    {
        if (sorted.Count == 0)
            throw new ArgumentException("Sample is empty.", nameof(sorted));
        if (percent <= 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent));
        var rank = (int)Math.Ceiling(percent / 100 * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }

    /// <summary>Allocated bytes per input byte A.</summary>
    public static double AllocPerInputByte(long allocatedBytes, long inputBytes) =>
        inputBytes <= 0 ? 0 : (double)allocatedBytes / inputBytes;
}
