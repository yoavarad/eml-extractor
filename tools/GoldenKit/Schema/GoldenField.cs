using System.Text.Json.Serialization;

namespace GoldenKit.Schema;

public sealed class GoldenField
{
    public string? Eq { get; set; }
    [JsonConverter(typeof(StringOrListConverter))]
    public List<string> Contains { get; set; } = [];
    [JsonConverter(typeof(StringOrListConverter))]
    public List<string> NotContains { get; set; } = [];
}
