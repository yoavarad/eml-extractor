namespace GoldenKit.Schema;

public sealed class GoldenExpect
{
    public Dictionary<string, GoldenField> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public GoldenField? XFields { get; set; }
    public GoldenField? MainContent { get; set; }
    public string? MainContentMimeType { get; set; }
    public int? AttachmentCount { get; set; }
    public List<GoldenAttachment> Attachments { get; set; } = [];
}
