namespace EmlExtraction.Bench.Sweep;

/// <summary>Loads the sweep input sets fully into memory before any timing (ADR-015).</summary>
internal static class SweepInputs
{
    /// <summary>The five bench messages plus every golden <c>input.eml</c>.</summary>
    public static byte[][] Synthetic(string repoRoot)
    {
        var golden = Path.Combine(repoRoot, "corpus", "golden");
        if (!Directory.Exists(golden))
            throw new DirectoryNotFoundException($"Golden set not found at {golden}.");
        return Corpus.Create().Select(s => s.Bytes)
            .Concat(Directory.EnumerateFiles(golden, "input.eml", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllBytes))
            .ToArray();
    }

    /// <summary>Every non-empty file under <paramref name="path"/>, mbox files split per message; null when none.</summary>
    public static byte[][]? Public(string path)
    {
        if (!Directory.Exists(path))
            return null;
        var messages = new List<byte[]>();
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length == 0)
                continue;
            if (MboxSplitter.IsMbox(file, bytes))
                messages.AddRange(MboxSplitter.Split(bytes));
            else
                messages.Add(bytes);
        }

        return messages.Count == 0 ? null : messages.ToArray();
    }

    /// <summary>Both sets, shuffled with a fixed seed so runs are repeatable.</summary>
    public static byte[][] Mix(byte[][] synthetic, byte[][] publicSet)
    {
        var mix = synthetic.Concat(publicSet).ToArray();
        new Random(48).Shuffle(mix);
        return mix;
    }
}

internal static class RepoRoot
{
    /// <summary>Nearest ancestor of the binary (then of the working directory) that holds EmlExtraction.slnx.</summary>
    public static string Find()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EmlExtraction.slnx")))
                    return dir.FullName;
            }
        }

        return Environment.CurrentDirectory;
    }
}
