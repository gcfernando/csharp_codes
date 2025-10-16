using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Logging;

namespace LogFunction.Benchmark;

[MemoryDiagnoser(displayGenColumns: true)]
public class InitializationBenchmarks
{
    [Benchmark(Baseline = true)]
    public ILogger CreateILogger() =>
        LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Default");

    [Benchmark]
    public ILogger CreateExLogger() =>
        LoggerFactory.Create(b => b.AddConsole()).CreateLogger("ExLogger");
}
