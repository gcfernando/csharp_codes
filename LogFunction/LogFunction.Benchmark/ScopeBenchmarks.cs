using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using LogFunction.Logger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogFunction.Benchmark;

[MemoryDiagnoser(displayGenColumns: true)]
public class ScopeBenchmarks
{
    private readonly ILogger _logger = NullLogger.Instance;

    [Benchmark]
    public void BeginScope_SingleKey()
    {
        using var _ = _logger.ExBeginScope("OperationId", Guid.NewGuid());
    }

    [Benchmark]
    public void BeginScope_MultiKey()
    {
        using var _ = _logger.ExBeginScope(new Dictionary<string, object>
        {
            ["User"] = "Admin",
            ["Session"] = Guid.NewGuid(),
            ["IP"] = "127.0.0.1"
        });
    }
}
