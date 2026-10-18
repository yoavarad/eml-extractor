namespace GoldenKit.Schema;

public sealed class GoldenAttachment
{
    public string? Name { get; set; }
    public string? NameContains { get; set; }
    public long? Size { get; set; }
    public string? Sha256 { get; set; }
    public string? Disposition { get; set; }
    public string? FullPath { get; set; }
    public string? OriginalPath { get; set; }
    public string? ContentLocation { get; set; }
    public GoldenExpect? Reextract { get; set; }
}
