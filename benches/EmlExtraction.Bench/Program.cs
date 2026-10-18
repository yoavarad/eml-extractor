using BenchmarkDotNet.Running;
using EmlExtraction.Bench;

if (args.Contains("--bdn"))
{
    BenchmarkRunner.Run<EmlBenchmarks>();
    return 0;
}

ThroughputRunner.Run();
return 0;
