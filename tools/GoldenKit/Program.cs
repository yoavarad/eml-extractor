using EmlExtraction.Logics;
using GoldenKit;

var repo = FindRepo(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
var fault = Array.IndexOf(args, "--inject-fault");
if (fault >= 0)
{
    if (fault + 1 >= args.Length || args[fault + 1] is not ("parse" or "metadata" or "body" or "attachments"))
    {
        Console.Error.WriteLine("--inject-fault needs one of: parse, metadata, body, attachments");
        return 2;
    }

    // ADR-023 fault seam: proves score/probe exit non-zero when extraction throws.
    var stage = args[fault + 1];
    EmlExtractor.StageFault = s =>
    {
        if (s == stage)
            throw new InvalidCastException("GoldenKit --inject-fault");
    };
    args = args.Where((_, i) => i != fault && i != fault + 1).ToArray();
}

var cmd = args.Length > 0 ? args[0] : "help";
switch (cmd)
{
    case "gen":
        Console.WriteLine(Generator.Run(repo));
        return 0;
    case "score":
        return Scorer.Run(repo);
    case "probe":
        return Probe.Run(repo, args.Skip(1).FirstOrDefault());
    case "inspect":
        return Inspect.Run(repo, args.Skip(1));
    default:
        Console.WriteLine("Usage: golden gen | golden score | golden probe [dir] | golden inspect <path>…");
        return 2;
}

static string? FindRepo(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "EmlExtraction.slnx")))
            return dir.FullName;
        dir = dir.Parent;
    }

    return null;
}
