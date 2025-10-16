using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using LogFunction.Logger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogFunction.Benchmark;

[GcServer(true)]
[ThreadingDiagnoser]
[MemoryDiagnoser(displayGenColumns: true)]
public class ExceptionBenchmarks
{
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly Exception _simple = new InvalidOperationException("Simple failure");
    private readonly Exception _deep;

    public ExceptionBenchmarks()
    {
        _deep = new AggregateException("Aggregate",
            new InvalidOperationException("Level 1",
                new NullReferenceException("Level 2",
                    new ArgumentException("Level 3"))));
    }

    [Benchmark]
    public void Format_Simple() => ExLogger.ExLogErrorException(_logger, _simple, "Simple error", moreDetailsEnabled: true);

    [Benchmark]
    public void Format_Deep() => ExLogger.ExLogErrorException(_logger, _deep, "Deep error", moreDetailsEnabled: true);
}