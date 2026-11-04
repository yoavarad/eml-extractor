using System.Text.Json;
using GoldenKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-026: the probe report shows internal_error next to thrown.</summary>
public sealed class StatsTests
{
    [Fact]
    public void ReportsInternalErrorsNextToThrown()
    {
        var stats = new Stats { Thrown = 1, InternalErrors = 2 };

        Assert.Contains("thrown=1 internal_error=2 ", stats.ToText("r", 0, TimeSpan.Zero), StringComparison.Ordinal);
        using var json = JsonDocument.Parse(stats.ToJson("r", 0, TimeSpan.Zero));
        Assert.Equal(2, json.RootElement.GetProperty("InternalErrors").GetInt32());
    }
}
