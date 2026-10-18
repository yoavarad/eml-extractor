using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        var spec = JsonSerializer.Deserialize<GoldenSpecFile>(File.ReadAllText(expectedPath), JsonOptions)
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
                Assert.Equal(size, match.SizeInBytes);
            if (wanted.Sha256 is string sha)
                Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(match.BinaryData)), StringComparer.OrdinalIgnoreCase);
            if (wanted.Disposition is string disp)
                Assert.Equal(disp, match.Disposition);
            if (wanted.OriginalPath is string path)
                Assert.Equal(path, match.OriginalPath);
            if (wanted.ContentLocation is string loc)
                Assert.Equal(loc, match.ContentLocation);
            if (wanted.Reextract is not null)
                Check(_extractor.Extract(match.BinaryData), wanted.Reextract, $"{label}/{match.FileName}", depth + 1);
        }
    }

    private static bool Matches(ExtractedAttachment actual, GoldenAttachment wanted)
    {
        if (wanted.Name is not null)
            return actual.FileName.Equals(wanted.Name, StringComparison.OrdinalIgnoreCase);
        return wanted.NameContains is not null
               && actual.FileName.Contains(wanted.NameContains, StringComparison.OrdinalIgnoreCase);
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
        // Resolved from the nearest repo root (walk up from the test output), like the bulk set in GoldenKit.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "corpus", "golden")))
            dir = dir.Parent;
        if (dir is null)
            yield break;
        yield return Path.Combine(dir.FullName, "corpus", "golden", "seed");
        yield return Path.Combine(dir.FullName, "corpus", "internal", "golden");
    }
}
