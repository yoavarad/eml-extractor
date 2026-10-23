using EmlExtraction.Bench.Sweep;
using MimeKit;
using MimeKit.Tnef;

namespace EmlExtraction.Bench.Profile;

/// <summary>
/// Fixed per-stage inputs (ADR-020): the five bench messages plus real samples picked by header count,
/// attachment count and TNEF presence. Names are <c>synthetic/&lt;name&gt;</c> or
/// <c>corpus/&lt;relative path&gt;[#&lt;mbox index&gt;]</c>; child processes reload by name from the corpus in
/// <see cref="CorpusEnvVar"/>.
/// </summary>
public static class ProfileSamples
{
    public const string CorpusEnvVar = "EML_PROFILE_CORPUS";

    private static IReadOnlyList<string>? _names;

    /// <summary>Benchmark parameter values. Evaluated in the host; scans the corpus once.</summary>
    public static IEnumerable<string> Names() => _names ??= Pick();

    internal static byte[] Load(string name)
    {
        if (name.StartsWith("synthetic/", StringComparison.Ordinal))
            return Corpus.Create().Single(s => s.Name == name["synthetic/".Length..]).Bytes;
        if (!name.StartsWith("corpus/", StringComparison.Ordinal))
            throw new ArgumentException($"Unknown sample '{name}'.", nameof(name));

        var rest = name["corpus/".Length..];
        var hash = rest.LastIndexOf('#');
        var file = Path.Combine(CorpusPath(), hash < 0 ? rest : rest[..hash]);
        var bytes = File.ReadAllBytes(file);
        if (hash < 0)
            return bytes;
        var index = int.Parse(rest[(hash + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        return MboxSplitter.Split(bytes).ElementAt(index);
    }

    private static string CorpusPath() =>
        Environment.GetEnvironmentVariable(CorpusEnvVar)
        ?? throw new InvalidOperationException($"{CorpusEnvVar} is not set.");

    private static List<string> Pick()
    {
        var names = Corpus.Create().Select(s => "synthetic/" + s.Name).ToList();
        var corpus = Environment.GetEnvironmentVariable(CorpusEnvVar);
        if (corpus is null || !Directory.Exists(corpus))
            return names;

        var features = new List<SampleFeatures>();
        foreach (var (name, bytes) in Enumerate(corpus))
        {
            using var input = MimeMessageParser.Open(bytes);
            MimeMessage message;
            try
            {
                message = new MimeMessageParser().Parse(input, bytes);
            }
            catch (Exception)
            {
                continue; // unparseable samples are never representative picks
            }

            var attachments = PartCollector.Collect(message.Body, BodySelector.Select(message.Body), []).Count;
            features.Add(new SampleFeatures(name, bytes.Length, message.Headers.Count, attachments, message.BodyParts.OfType<TnefPart>().Any()));
        }

        var picks = ProfileMath.PickSamples(features);
        foreach (var pick in picks)
        {
            var f = features.Single(s => s.Name == pick);
            Console.WriteLine($"picked {f.Name}: {f.Bytes} bytes, {f.Headers} headers, {f.Attachments} attachments, tnef={f.HasTnef}");
        }

        if (!features.Any(f => f.HasTnef))
            Console.WriteLine("picked: no TNEF sample in corpus");
        names.AddRange(picks);
        return names;
    }

    private static IEnumerable<(string Name, byte[] Bytes)> Enumerate(string corpus)
    {
        foreach (var file in Directory.EnumerateFiles(corpus, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length == 0)
                continue;
            var rel = "corpus/" + Path.GetRelativePath(corpus, file).Replace(Path.DirectorySeparatorChar, '/');
            if (!MboxSplitter.IsMbox(file, bytes))
            {
                yield return (rel, bytes);
                continue;
            }

            var i = 0;
            foreach (var message in MboxSplitter.Split(bytes))
                yield return ($"{rel}#{i++}", message);
        }
    }
}
