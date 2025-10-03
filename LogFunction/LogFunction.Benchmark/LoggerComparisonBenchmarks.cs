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
public class LoggerComparisonBenchmarks
{
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly BatchLogger _batchingLogger;
    private readonly Exception _ex = new InvalidOperationException("Test exception");

    public LoggerComparisonBenchmarks() =>
        _batchingLogger = new BatchLogger(NullLogger.Instance, capacity: 5000, batchSize: 50, flushInterval: TimeSpan.FromMilliseconds(50));

    // ---- Information ----
    [Benchmark(Baseline = true)]
    public void ILogger_LogInformation() =>
        _logger.LogInformation("User {UserId} logged in", 42);

    [Benchmark]
    public void ExLogger_LogInformation() =>
        ExLogger.LogInformation(_logger, "User {UserId} logged in", 42);

    [Benchmark]
    public void BatchingLogger_LogInformation() =>
        _batchingLogger.LogInformation("User {UserId} logged in", null, 42);

    // ---- Error (with exception) ----
    [Benchmark]
    public void ILogger_LogError() =>
        _logger.LogError(_ex, "Error for user {UserId}", 42);

    [Benchmark]
    public void ExLogger_LogError() =>
        ExLogger.LogError(_logger, "Error for user {UserId}", _ex, 42);

    [Benchmark]
    public void BatchingLogger_LogError() =>
        _batchingLogger.LogError("Error for user {UserId}", _ex, 42);

    // ---- Trace ----
    [Benchmark]
    public void ILogger_LogTrace() =>
        _logger.LogTrace("Trace message");

    [Benchmark]
    public void ExLogger_LogTrace() =>
        ExLogger.LogTrace(_logger, "Trace message");

    [Benchmark]
    public void BatchingLogger_LogTrace() =>
        _batchingLogger.LogTrace("Trace message");

    [GlobalCleanup]
    public void Cleanup() => _batchingLogger.Dispose();
}