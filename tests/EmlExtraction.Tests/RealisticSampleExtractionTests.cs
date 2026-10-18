using System.Security.Cryptography;
using System.Text.Json;

namespace EmlExtraction.Tests;

/// <summary>Data-driven extraction checks over every committed corpus/realistic sample (manifest.json). Missing files fail, never skip.</summary>
public class RealisticSampleExtractionTests
{
    static readonly string Dir = FindDir();

    static string FindDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EmlExtraction.slnx"))) d = d.Parent;
        return Path.Combine(d?.FullName ?? throw new InvalidOperationException("repo root not found"), "corpus", "realistic");
    }

    public static IEnumerable<object[]> Samples()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "manifest.json")));
        foreach (var s in doc.RootElement.GetProperty("samples").EnumerateArray())
            yield return [s.GetProperty("file").GetString()!, s.GetProperty("expected").GetRawText(), s.GetProperty("sha256").GetString()!];
    }

    static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    [Fact]
    public void EveryCommittedEmlHasManifestRow()
    {
        var rows = Samples().Select(r => (string)r[0]).Order().ToList();
        var onDisk = Directory.GetFiles(Dir, "*.eml").Select(p => Path.GetFileName(p)).Order().ToList();
        Assert.Equal(onDisk, rows);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void SampleExtractsToManifestExpectation(string file, string expectedJson, string fileSha256)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Dir, file));
        Assert.Equal(fileSha256, Sha(bytes));
        using var doc = JsonDocument.Parse(expectedJson);
        var exp = doc.RootElement;

        Assert.True(new EmlDetector().IsEml(bytes), $"{file} not detected");
        var result = new CheckedExtractor().Extract(bytes);

        var expected = new List<(string Name, long Size, string Sha, string Path)>();
        if (exp.TryGetProperty("attachments", out var atts))
            foreach (var a in atts.EnumerateArray()) expected.Add(Row(a));
        if (exp.TryGetProperty("inlineImages", out var imgs))
            foreach (var i in imgs.EnumerateArray()) expected.Add(Row(i.GetProperty("file")));

        Assert.Equal(expected.Select(e => e.Path).Order(StringComparer.Ordinal),
            result.Attachments.Select(a => a.FullPath).Order(StringComparer.Ordinal));
        foreach (var e in expected)
        {
            var att = result.Attachments.Single(a => a.FullPath == e.Path);
            Assert.Equal(e.Name, att.FileName);
            Assert.Equal(e.Size, att.SizeInBytes);
            Assert.Equal(e.Size, att.BinaryData.Length);
            Assert.Equal(e.Sha, Sha(att.BinaryData));
        }

        if (exp.TryGetProperty("inlineImages", out var inline))
            foreach (var i in inline.EnumerateArray())
            {
                var cid = i.GetProperty("contentId").GetString()!;
                var att = result.Attachments.Single(a => a.FullPath == i.GetProperty("file").GetProperty("fullPath").GetString());
                Assert.Equal(cid, att.ContentId);
                Assert.Equal("text/html", result.MainContentMimeType);
                Assert.Contains("cid:" + cid, result.MainContent);
            }

        if (file == "tnef-docx.eml")
        {
            Assert.DoesNotContain(result.Attachments, a => a.FileName == "winmail.dat");
            Assert.All(result.Attachments, a => Assert.StartsWith("winmail.dat/", a.FullPath));
        }
        if (file == "attached-eml.eml")
            Assert.Single(result.Attachments); // nested message stays one opaque attachment
    }

    static (string, long, string, string) Row(JsonElement a) =>
        (a.GetProperty("fileName").GetString()!, a.GetProperty("sizeInBytes").GetInt64(),
         a.GetProperty("sha256").GetString()!, a.GetProperty("fullPath").GetString()!);
}
