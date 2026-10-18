namespace GoldenKit;

public sealed class CaseDef
{
    public required string Id { get; init; }
    public required string[] Tags { get; init; }
    public required byte[] Eml { get; init; }
    public bool IsEml { get; init; } = true;
    public GoldenExpect Expect { get; init; } = new();
}
