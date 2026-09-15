using GoldenKit;

var repo = FindRepo(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
var cmd = args.Length > 0 ? args[0] : "help";
switch (cmd)
{
    case "gen":
        Console.WriteLine(Generator.Run(repo));
        return 0;
    case "score":
        return Scorer.Run(repo);
    default:
        Console.WriteLine("Usage: golden gen | golden score");
        return 2;
}

static string? FindRepo(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "docs", "golden-1200-plan.md"))
            || File.Exists(Path.Combine(dir.FullName, "EmlExtraction.slnx")))
            return dir.FullName;
        dir = dir.Parent;
    }

    return null;
}
