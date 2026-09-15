using System.Text.Json;

namespace GoldenKit;

internal static class Generator
{
    public static string Run(string repoRoot)
    {
        var cases = Catalog.All();
        var bulk = Path.Combine(repoRoot, "corpus", "golden", "bulk");
        if (Directory.Exists(bulk))
            Directory.Delete(bulk, recursive: true);
        Directory.CreateDirectory(bulk);

        var catalogPath = Path.Combine(repoRoot, "corpus", "golden", "catalog.jsonl");
        using var catalog = new StreamWriter(catalogPath);
        foreach (var c in cases)
        {
            var dir = Path.Combine(bulk, c.Id);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "input.eml"), c.Eml);
            var spec = new GoldenSpecFile
            {
                Id = c.Id,
                Tags = c.Tags,
                IsEml = c.IsEml,
                Expect = c.Expect
            };
            File.WriteAllText(Path.Combine(dir, "expected.json"), JsonSerializer.Serialize(spec, Json.Options));
            catalog.WriteLine(JsonSerializer.Serialize(new
            {
                id = c.Id,
                tags = c.Tags,
                is_eml = c.IsEml,
                bytes = c.Eml.Length
            }));
        }

        return $"Wrote {cases.Count} cases to {bulk}";
    }
}
