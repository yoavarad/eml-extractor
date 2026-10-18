using System.Collections.Concurrent;
using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace EmlExtraction.Bench.Profile;

/// <summary>
/// Attachment copy factor (ADR-020): allocated bytes/op of <see cref="AttachmentsStage"/> (MemoryDiagnoser,
/// measured with <c>GC.GetAllocatedBytesForCurrentThread</c> around the stage in the benchmark process, per runtime)
/// divided by the decoded attachment bytes it returns. Counts every allocation, so it is an upper bound on the
/// byte[] copy factor; the GcVerbose trace splits it by type.
/// </summary>
internal sealed class CopyFactorColumn : IColumn
{
    private readonly ConcurrentDictionary<string, long> _decoded = new(StringComparer.Ordinal);

    public string Id => nameof(CopyFactorColumn);
    public string ColumnName => "CopyFactor";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Attachments stage: allocated bytes/op / decoded attachment bytes returned (1.0 = one allocation per decoded byte)";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, summary.Style);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        if (benchmarkCase.Descriptor.Type != typeof(AttachmentsStage))
            return "-";
        var allocated = summary[benchmarkCase]?.GcStats.GetBytesAllocatedPerOperation(benchmarkCase);
        if (allocated is null)
            return "?";
        var sample = (string)benchmarkCase.Parameters[nameof(StageBenchmark.Sample)];
        var factor = ProfileMath.CopyFactor(allocated.Value, _decoded.GetOrAdd(sample, AttachmentsStage.DecodedBytes));
        return factor is null ? "n/a" : factor.Value.ToString("F2", CultureInfo.InvariantCulture);
    }

    public bool IsAvailable(Summary summary) => true;

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
}
