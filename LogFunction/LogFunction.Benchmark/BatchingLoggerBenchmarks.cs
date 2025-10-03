using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using LogFunction.Logger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogFunction.Benchmark;

[GcServer(true)]
[GcForce(true)]
[ThreadingDiagnoser]
[MemoryDiagnoser(displayGenColumns: true)]
public class BatchingLoggerAllMethodsBenchmarks
{
    private readonly BatchLogger _logger;
    private readonly Exception _ex = new InvalidOperationException("Batch error");

    public BatchingLoggerAllMethodsBenchmarks() =>
        _logger = new BatchLogger(NullLogger.Instance, capacity: 5000, batchSize: 50, flushInterval: TimeSpan.FromMilliseconds(50));

    [Benchmark] public void LogTrace() => _logger.LogTrace("Trace {Value}", null, 1);
    [Benchmark] public void LogDebug() => _logger.LogDebug("Debug {Value}", null, 2);
    [Benchmark] public void LogInformation() => _logger.LogInformation("Info {Value}", null, 3);
    [Benchmark] public void LogWarning() => _logger.LogWarning("Warn {Value}", null, 4);
    [Benchmark] public void LogError_NoException() => _logger.LogError("Error {Value}", null, 5);
    [Benchmark] public void LogError_WithException() => _logger.LogError("Error with exception", _ex);
    [Benchmark] public void LogCritical() => _logger.LogCritical("Critical {Value}", _ex, 99);

    [GlobalCleanup]
    public void Cleanup() => _logger.Dispose();
}