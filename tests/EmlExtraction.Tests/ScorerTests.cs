using System.Text;
using GoldenKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-026: GoldenKit score fails a case and exits non-zero on an internal_error warning (fault seam).</summary>
[Collection("NonParallel")]
public sealed class ScorerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "goldenkit-score-" + Guid.NewGuid().ToString("N"));

    public ScorerTests()
    {
        var golden = Path.Combine(_root, "corpus", "golden", "g1");
        Directory.CreateDirectory(golden);
        File.WriteAllText(Path.Combine(golden, "expected.json"), """{ "id": "g1", "is_eml": true }""");
        File.WriteAllText(Path.Combine(golden, "input.eml"), "From: a@example.com\r\nSubject: hi\r\n\r\nbody\r\n", Encoding.ASCII);
    }

    public void Dispose()
    {
        InternalErrors.Injected = null;
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void PassesWithoutInternalError() => Assert.Equal(0, Scorer.Run(_root));

    [Fact]
    public void FailsCaseOnInternalError()
    {
        InternalErrors.Injected = "internal_error: metadata: InvalidCastException";

        Assert.Equal(1, Scorer.Run(_root));
        Assert.Contains("no_throw (internal_error: metadata: InvalidCastException)", File.ReadAllText(Path.Combine(_root, "docs", "golden-score.txt")), StringComparison.Ordinal);
    }
}
