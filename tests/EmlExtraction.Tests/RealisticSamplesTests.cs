using System.Text.Json;
using MimeKit;
using MimeKit.Tnef;
using RealisticSamples;

namespace EmlExtraction.Tests;

/// <summary>Generator self-check and manifest schema for corpus/realistic (ADR-018). Extraction assertions live elsewhere.</summary>
public class RealisticSamplesTests
{
    static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EmlExtraction.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    static string Committed => Path.Combine(RepoRoot(), "corpus", "realistic");

    static string Gen()
    {
        var dir = Path.Combine(Path.GetTempPath(), "realistic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        SampleGenerator.Generate(dir);
        return dir;
    }

    [Fact]
    public void Generator_IsByteIdenticalAcrossRunsAndMatchesCommitted()
    {
        var a = Gen();
        var b = Gen();
        try
        {
            var names = Directory.GetFiles(a).Select(Path.GetFileName).Order().ToList();
            Assert.Equal(names, Directory.GetFiles(b).Select(Path.GetFileName).Order().ToList());
            foreach (var n in names)
            {
                var bytes = File.ReadAllBytes(Path.Combine(a, n!));
                Assert.True(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(b, n!))), $"{n} differs between runs");
                Assert.True(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Committed, n!))), $"{n} differs from committed; re-run tools/RealisticSamples");
            }
        }
        finally { Directory.Delete(a, true); Directory.Delete(b, true); }
    }

    [Fact]
    public void Manifest_HasRequiredFieldsAndCoversAllKinds()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Committed, "manifest.json")));
        var samples = doc.RootElement.GetProperty("samples").EnumerateArray().ToList();
        Assert.NotEmpty(samples);
        foreach (var s in samples)
        {
            foreach (var k in new[] { "file", "generator", "license", "sha256", "expected" })
                Assert.True(s.TryGetProperty(k, out _), $"missing {k}");
            var path = Path.Combine(Committed, s.GetProperty("file").GetString()!);
            Assert.Equal(s.GetProperty("sha256").GetString(), SampleGenerator.Sha(File.ReadAllBytes(path)));
        }
        var files = samples.Select(s => s.GetProperty("file").GetString()!).ToHashSet();
        foreach (var f in new[] { "attached-docx", "attached-xlsx", "attached-pptx", "attached-pdf", "attached-eml", "tnef-docx", "attached-image", "outlook-paste", "gmail-paste" })
            Assert.Contains(f + ".eml", files);
    }

    [Fact]
    public void Expected_MatchesIndependentParseOfSampleFiles()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Committed, "manifest.json")));
        foreach (var s in doc.RootElement.GetProperty("samples").EnumerateArray())
        {
            var msg = MimeMessage.Load(Path.Combine(Committed, s.GetProperty("file").GetString()!));
            var exp = s.GetProperty("expected");
            if (exp.TryGetProperty("attachments", out var atts))
                foreach (var a in atts.EnumerateArray())
                {
                    var name = a.GetProperty("fileName").GetString()!;
                    var data = Find(msg, name, a.GetProperty("fullPath").GetString()!);
                    Assert.Equal(a.GetProperty("sizeInBytes").GetInt64(), data.Length);
                    Assert.Equal(a.GetProperty("sha256").GetString(), SampleGenerator.Sha(data));
                }
            if (exp.TryGetProperty("inlineImages", out var imgs))
                foreach (var i in imgs.EnumerateArray())
                {
                    var part = msg.BodyParts.OfType<MimePart>().Single(p => p.ContentId == i.GetProperty("contentId").GetString());
                    var data = Bytes(part);
                    Assert.Equal(i.GetProperty("file").GetProperty("sha256").GetString(), SampleGenerator.Sha(data));
                    Assert.Contains("cid:" + part.ContentId, msg.HtmlBody);
                }
        }
    }

    static byte[] Bytes(MimePart p)
    {
        using var ms = new MemoryStream();
        p.Content.DecodeTo(ms);
        return ms.ToArray();
    }

    static byte[] Find(MimeMessage msg, string name, string fullPath)
    {
        if (fullPath.StartsWith("winmail.dat/"))
        {
            var tp = msg.BodyParts.OfType<TnefPart>().Single();
            var inner = tp.ExtractAttachments().OfType<MimePart>().Single(p => p.FileName == name);
            return Bytes(inner);
        }
        foreach (var p in msg.BodyParts)
        {
            if (p is MessagePart mp && mp.ContentDisposition?.FileName == name)
            {
                using var ms = new MemoryStream();
                mp.Message.WriteTo(ms);
                return ms.ToArray();
            }
            if (p is MimePart m && m.FileName == name) return Bytes(m);
        }
        throw new InvalidOperationException($"{name} not found");
    }
}
