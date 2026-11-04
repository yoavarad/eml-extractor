using EmlExtraction.Models;

namespace GoldenKit;

/// <summary>ADR-026 gate: an internal_error warning is a library bug, so score and probe fail on it.</summary>
internal static class InternalErrors
{
    public const string Code = "internal_error";

    /// <summary>
    /// Fault seam (<c>--inject-fault internal_error</c>): appended to every extraction result to prove the gate.
    /// Process-wide; in tests only GoldenKitGateTests may set it.
    /// </summary>
    public static string? Injected { get; set; }

    public static EmlExtractionResult Apply(EmlExtractionResult result)
        => Injected is null ? result : result with { Warnings = [.. result.Warnings, Injected] };

    public static bool Is(string warning) => warning.StartsWith(Code + ":", StringComparison.Ordinal);
}
