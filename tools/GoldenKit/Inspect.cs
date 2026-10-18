using EmlExtraction;

namespace GoldenKit;

internal static class Inspect
{
    public static int Run(string repo, IEnumerable<string> relativePaths)
    {
        var extractor = new EmlExtractor();
        var paths = relativePaths.ToList();
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("Usage: golden inspect <path> [path...]");
            return 2;
        }

        foreach (var rel in paths)
        {
            var path = Path.IsPathRooted(rel) ? rel : Path.Combine(repo, rel);
            if (!File.Exists(path))
            {
                Console.WriteLine($"MISSING {path}");
                continue;
            }

            var result = extractor.Extract(File.ReadAllBytes(path));
            Console.WriteLine($"== {Path.GetRelativePath(repo, path)} ==");
            Console.WriteLine($"  warnings={result.Warnings.Count} mime={result.MainContentMimeType} body_len={result.MainContent?.Length ?? 0} attachments={result.Attachments.Count} headers={result.Metadata.Count} xfields={result.Metadata["xfields"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Length}");
            if (!string.IsNullOrWhiteSpace(result.Metadata.GetValueOrDefault("subject")))
                Console.WriteLine($"  subject={result.Metadata["subject"]}");
            if (!string.IsNullOrWhiteSpace(result.MainContent))
            {
                var snippet = result.MainContent.Replace('\n', ' ');
                if (snippet.Length > 180)
                    snippet = snippet[..180] + "…";
                Console.WriteLine($"  body={snippet}");
            }

            foreach (var warning in result.Warnings)
                Console.WriteLine($"  WARN {warning}");

            foreach (var a in result.Attachments)
            {
                var magic = a.Data.Length >= 4 ? Convert.ToHexString(a.Data.AsSpan(0, Math.Min(8, a.Data.Length))) : "";
                Console.WriteLine($"  attach name={a.Name} size={a.Size} cid={a.ContentId} disp={a.Disposition} path={a.FullPath} magic={magic}");
            }
        }

        return 0;
    }
}
