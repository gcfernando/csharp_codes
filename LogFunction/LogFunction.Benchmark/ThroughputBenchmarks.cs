using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using LogFunction.Logger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogFunction.Benchmark;

[GcServer(true)]
[ThreadingDiagnoser]
[MemoryDiagnoser(displayGenColumns: true)]
public class ThroughputBenchmarks
{
    private readonly ILogger _logger = NullLogger.Instance;

    [Params(1, 2, 4, 8, 16)]
    public int Threads { get; set; }

    [Benchmark(Description = "Parallel ExLogger logging test")]
    public Task ParallelExLogger()
    {
        var tasks = new Task[Threads];
        for (int i = 0; i < Threads; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 10_000; j++)
                {
                    ExLogger.ExLogInformation(_logger, "Parallel log {Index}", j);
                }
            });
        }

        return Task.WhenAll(tasks);
    }
}