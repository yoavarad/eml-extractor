using EmlExtraction.Logics;

namespace EmlExtraction.Tests;

/// <summary>Source layout: each type lives in the sub-namespace matching its folder (ADR-012 amendment, task 149).</summary>
public sealed class NamespaceLayoutTests
{
    private static readonly Dictionary<string, string> PublicLayout = new()
    {
        ["IEmlExtractor"] = "EmlExtraction.Abstractions",
        ["IEmlDetector"] = "EmlExtraction.Abstractions",
        ["EmlExtractionRequest"] = "EmlExtraction.Models",
        ["EmlExtractionResult"] = "EmlExtraction.Models",
        ["EmlDetectionResult"] = "EmlExtraction.Models",
        ["ExtractedAttachment"] = "EmlExtraction.Models",
        ["EmlExtractionException"] = "EmlExtraction.Exceptions",
        ["EmlInputTooLargeException"] = "EmlExtraction.Exceptions",
        ["EmlExtractor"] = "EmlExtraction.Logics",
        ["EmlDetector"] = "EmlExtraction.Logics",
    };

    private static readonly string[] AllowedNamespaces =
    [
        "EmlExtraction.Abstractions",
        "EmlExtraction.Models",
        "EmlExtraction.Exceptions",
        "EmlExtraction.Logics",
    ];

    [Fact]
    public void PublicTypesMatchAgreedLayout()
    {
        var actual = typeof(EmlExtractor).Assembly.GetExportedTypes()
            .Where(t => !t.IsNested)
            .ToDictionary(t => t.Name, t => t.Namespace ?? "");

        Assert.Equal(PublicLayout.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));
    }

    [Fact]
    public void NoTypeSitsInTheRootNamespace()
    {
        var misplaced = typeof(EmlExtractor).Assembly.GetTypes()
            .Where(t => !t.IsNested && t.Namespace is not null && t.Namespace.StartsWith("EmlExtraction", StringComparison.Ordinal))
            .Where(t => !AllowedNamespaces.Contains(t.Namespace))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(misplaced);
    }

    [Fact]
    public void InternalTypesSitInLogics()
    {
        var misplaced = typeof(EmlExtractor).Assembly.GetTypes()
            .Where(t => !t.IsNested && !t.IsPublic && t.Namespace is not null && t.Namespace.StartsWith("EmlExtraction", StringComparison.Ordinal))
            .Where(t => t.Namespace != "EmlExtraction.Logics")
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(misplaced);
    }

    [Fact]
    public void TelemetryNameIsUnchanged() => Assert.Equal("EmlExtraction", EmlExtractionTelemetry.Name);
}
