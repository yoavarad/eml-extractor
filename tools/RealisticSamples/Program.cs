using RealisticSamples;

// Usage: RealisticSamples [outDir]   (default corpus/realistic, found by walking up from cwd)
// Writes the sample .eml files and manifest.json. Output is deterministic.
var outDir = args.Length > 0 ? args[0] : Path.Combine(FindRoot(), "corpus", "realistic");
Directory.CreateDirectory(outDir);
SampleGenerator.Generate(outDir);
Console.WriteLine($"Wrote samples to {outDir}");
return 0;

static string FindRoot()
{
    var d = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "EmlExtraction.slnx"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("repo root not found; pass outDir");
}
