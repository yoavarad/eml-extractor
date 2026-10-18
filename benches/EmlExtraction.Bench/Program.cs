using BenchmarkDotNet.Running;
using EmlExtraction.Bench;
using EmlExtraction.Bench.Profile;
using EmlExtraction.Bench.Sweep;

if (args.Length > 0 && args[0] == "sweep")
    return SweepCommand.Run(args[1..]);
if (args.Length > 0 && args[0] == "large")
    return LargeFileCommand.Run(args[1..]);
if (args.Length > 0 && args[0] == "profile")
    return ProfileCommand.Run(args[1..]);
if (args.Length > 0 && args[0] == "profile-trace")
    return ProfileTraceCommand.Run(args[1..]);
if (args.Length > 0 && args[0] == "sweep-child")
    return SweepCommand.Child(args[1..]);

if (args.Contains("--bdn"))
{
    BenchmarkRunner.Run<EmlBenchmarks>();
    return 0;
}

ThroughputRunner.Run();
return 0;
