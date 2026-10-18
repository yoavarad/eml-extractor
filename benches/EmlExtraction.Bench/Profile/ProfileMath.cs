namespace EmlExtraction.Bench.Profile;

/// <summary>Shape of one real sample, used to pick the fixed per-stage inputs (ADR-020).</summary>
internal sealed record SampleFeatures(string Name, int Bytes, int Headers, int Attachments, bool HasTnef);

/// <summary>Pure parts of the per-stage profiling harness (ADR-020), unit-tested from the test project.</summary>
internal static class ProfileMath
{
    /// <summary>
    /// The real samples to profile: the one with most headers, the one with most attachments, and the largest
    /// with TNEF. Ties go to the larger sample, then the ordinal-first name. Distinct, in that order.
    /// </summary>
    public static IReadOnlyList<string> PickSamples(IEnumerable<SampleFeatures> samples)
    {
        var all = samples.OrderByDescending(s => s.Bytes).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();
        var picks = new List<string>();
        void Add(SampleFeatures? s)
        {
            if (s is not null && !picks.Contains(s.Name))
                picks.Add(s.Name);
        }

        Add(all.MaxBy(s => s.Headers));
        Add(all.Where(s => s.Attachments > 0).MaxBy(s => s.Attachments));
        Add(all.FirstOrDefault(s => s.HasTnef));
        return picks;
    }

    /// <summary>Allocated bytes in the attachments stage divided by decoded attachment bytes returned; null when none returned.</summary>
    public static double? CopyFactor(long allocatedBytes, long decodedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(decodedBytes);
        return decodedBytes == 0 ? null : (double)allocatedBytes / decodedBytes;
    }
}
