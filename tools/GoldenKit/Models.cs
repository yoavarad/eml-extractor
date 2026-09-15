using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoldenKit;

public sealed class CaseDef
{
    public required string Id { get; init; }
    public required string[] Tags { get; init; }
    public required byte[] Eml { get; init; }
    public bool IsEml { get; init; } = true;
    public GoldenExpect Expect { get; init; } = new();
}

public sealed class GoldenSpecFile
{
    public string Id { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public string Source { get; set; } = "generated-bulk";
    public bool IsEml { get; set; } = true;
    public GoldenExpect Expect { get; set; } = new();
}

public sealed class GoldenExpect
{
    public Dictionary<string, GoldenField> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public GoldenField? XFields { get; set; }
    public GoldenField? MainContent { get; set; }
    public string? MainContentMimeType { get; set; }
    public int? AttachmentCount { get; set; }
    public List<GoldenAttachment> Attachments { get; set; } = [];
}

public sealed class GoldenField
{
    public string? Eq { get; set; }
    public List<string> Contains { get; set; } = [];
    public List<string> NotContains { get; set; } = [];
}

public sealed class GoldenAttachment
{
    public string? Name { get; set; }
    public string? NameContains { get; set; }
    public long? Size { get; set; }
    public string? Sha256 { get; set; }
    public string? Disposition { get; set; }
    public string? FullPath { get; set; }
    public GoldenExpect? Reextract { get; set; }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
