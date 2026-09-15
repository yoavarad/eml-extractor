using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmlExtraction.Tests;

public sealed class GoldenTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly EmlDetector _detector = new();
    private readonly EmlExtractor _extractor = new();

    public static TheoryData<string> Cases()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var data = new TheoryData<string>();
        foreach (var path in Discover())
            data.Add(path);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SeedGoldenMatches(string expectedPath)
    {
        var spec = JsonSerializer.Deserialize<GoldenSpec>(File.ReadAllText(expectedPath), JsonOptions)
                   ?? throw new InvalidOperationException(expectedPath);
        var bytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(expectedPath)!, "input.eml"));
        var label = spec.Id;

        Assert.True(spec.IsEml == _detector.IsEml(bytes), $"{label} detector");
        if (!spec.IsEml)
            return;

        Check(_extractor.Extract(bytes), spec.Expect, label, depth: 0);
    }

    private void Check(EmlExtractionResult result, GoldenExpect expect, string label, int depth)
    {
        foreach (var (key, field) in expect.Metadata)
        {
            result.Metadata.TryGetValue(key, out var actual);
            CheckField($"{label} metadata.{key}", actual, field);
        }

        if (expect.XFields is not null)
            CheckField($"{label} xfields", result.XFields, expect.XFields);
        if (expect.MainContent is not null)
            CheckField($"{label} main_content", result.MainContent ?? "", expect.MainContent);
        if (expect.MainContentMimeType is not null)
            Assert.Equal(expect.MainContentMimeType, result.MainContentMimeType, StringComparer.OrdinalIgnoreCase);
        if (expect.AttachmentCount is int count)
            Assert.Equal(count, result.Attachments.Count);

        foreach (var wanted in expect.Attachments)
        {
            var match = result.Attachments.First(a => Matches(a, wanted));
            if (wanted.Size is long size)
                Assert.Equal(size, match.Size);
            if (wanted.Sha256 is string sha)
                Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(match.Data)), StringComparer.OrdinalIgnoreCase);
            if (wanted.Disposition is string disp)
                Assert.Equal(disp, match.Disposition);
            if (wanted.FullPath is string path)
                Assert.Equal(path, match.FullPath);
            if (wanted.Reextract is not null)
                Check(_extractor.Extract(match.Data), wanted.Reextract, $"{label}/{match.Name}", depth + 1);
        }
    }

    private static bool Matches(ExtractedAttachment actual, GoldenAttachment wanted)
    {
        if (wanted.Name is not null)
            return actual.Name.Equals(wanted.Name, StringComparison.OrdinalIgnoreCase);
        return wanted.NameContains is not null
               && actual.Name.Contains(wanted.NameContains, StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckField(string label, string? actual, GoldenField field)
    {
        actual ??= "";
        if (field.Eq is not null)
            Assert.True(actual == field.Eq, $"{label}: expected {field.Eq}");
        foreach (var part in field.Contains)
            Assert.True(actual.Contains(part, StringComparison.Ordinal), $"{label}: missing {part}");
        foreach (var part in field.NotContains)
            Assert.False(actual.Contains(part, StringComparison.Ordinal), $"{label}: unexpected {part}");
    }

    private static IEnumerable<string> Discover()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in Roots())
        {
            if (!Directory.Exists(root))
                continue;
            foreach (var file in Directory.EnumerateFiles(root, "expected.json", SearchOption.AllDirectories))
            {
                if (seen.Add(file))
                    yield return file;
            }
        }
    }

    private static IEnumerable<string> Roots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "golden");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, "corpus", "golden", "seed");
            yield return Path.Combine(dir.FullName, "corpus", "internal", "golden");
            dir = dir.Parent;
        }
    }
}

internal sealed class GoldenSpec
{
    public string Id { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public bool IsEml { get; set; } = true;
    public GoldenExpect Expect { get; set; } = new();
}

internal sealed class GoldenExpect
{
    public Dictionary<string, GoldenField> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public GoldenField? XFields { get; set; }
    public GoldenField? MainContent { get; set; }
    public string? MainContentMimeType { get; set; }
    public int? AttachmentCount { get; set; }
    public List<GoldenAttachment> Attachments { get; set; } = [];
}

internal sealed class GoldenField
{
    public string? Eq { get; set; }
    [JsonConverter(typeof(StringOrListConverter))]
    public List<string> Contains { get; set; } = [];
    [JsonConverter(typeof(StringOrListConverter))]
    public List<string> NotContains { get; set; } = [];
}

internal sealed class GoldenAttachment
{
    public string? Name { get; set; }
    public string? NameContains { get; set; }
    public long? Size { get; set; }
    public string? Sha256 { get; set; }
    public string? Disposition { get; set; }
    public string? FullPath { get; set; }
    public GoldenExpect? Reextract { get; set; }
}

internal sealed class StringOrListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return [reader.GetString() ?? ""];
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();
        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            list.Add(reader.GetString() ?? "");
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
