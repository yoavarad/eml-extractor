using System.Diagnostics;
using System.Text;

namespace EmlExtraction.Tests;

/// <summary>
/// GoldenKit CLI fault seam end to end: <c>--inject-fault internal_error</c> makes the probe exit 1 (ADR-026).
/// Runs the GoldenKit dll built next to this test assembly's configuration; skipped when it is not built.
/// </summary>
public sealed class ProgramTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "goldenkit-cli-" + Guid.NewGuid().ToString("N"));

    public ProgramTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "m1.eml"), "From: a@example.com\r\nSubject: hi\r\n\r\nbody\r\n", Encoding.ASCII);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(1, "--inject-fault", "internal_error")]
    [InlineData(2, "--inject-fault", "nope")]
    public void ProbeExitCodeFollowsTheFaultSeam(int expected, params string[] extra)
        => Assert.Equal(expected, RunGoldenKit(["probe", _root, .. extra]));

    private static int RunGoldenKit(string[] args)
    {
        // bin/<Configuration>/<tfm>/ of this test assembly -> the same configuration of tools/GoldenKit (net10.0 only).
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = baseDir.Parent!.Name;
        var repo = baseDir;
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "EmlExtraction.slnx")))
            repo = repo.Parent;
        Skip.If(repo is null, "repo root not found");
        var dll = Path.Combine(repo!.FullName, "tools", "GoldenKit", "bin", configuration, "net10.0", "GoldenKit.dll");
        Skip.IfNot(File.Exists(dll), $"GoldenKit not built: {dll}");

        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(dll);
        foreach (var a in args)
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
