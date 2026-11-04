using System.Text;
using System.Text.Json;
using GoldenKit;

namespace EmlExtraction.Tests;

/// <summary>ADR-026: the harvest probe counts internal_error warnings and exits non-zero when any (fault seam).</summary>
[Collection("NonParallel")]
public sealed class ProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "goldenkit-probe-" + Guid.NewGuid().ToString("N"));

    public ProbeTests()
    {
        var corpus = Path.Combine(_root, "corpus", "internal");
        Directory.CreateDirectory(corpus);
        File.WriteAllText(Path.Combine(corpus, "m1.eml"), "From: a@example.com\r\nSubject: hi\r\n\r\nbody\r\n", Encoding.ASCII);
    }

    public void Dispose()
    {
        InternalErrors.Injected = null;
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void PassesWithoutInternalError()
    {
        Assert.Equal(0, Probe.Run(_root, null));
        Assert.Equal(0, ReportInternalErrors());
    }

    [Fact]
    public void CountsInternalErrorAndFails()
    {
        InternalErrors.Injected = "internal_error: body: InvalidCastException";

        Assert.Equal(1, Probe.Run(_root, null));
        Assert.Equal(1, ReportInternalErrors());
    }

    private int ReportInternalErrors()
    {
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "corpus", "internal", "probe-report.json")));
        return report.RootElement.GetProperty("InternalErrors").GetInt32();
    }
}
