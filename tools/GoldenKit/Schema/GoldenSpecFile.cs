namespace GoldenKit.Schema;

// Golden schema shared by GoldenKit and EmlExtraction.Tests (linked source): one definition of expected.json.
public sealed class GoldenSpecFile
{
    public string Id { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public string Source { get; set; } = "generated-bulk";
    public bool IsEml { get; set; } = true;
    public GoldenExpect Expect { get; set; } = new();
}
