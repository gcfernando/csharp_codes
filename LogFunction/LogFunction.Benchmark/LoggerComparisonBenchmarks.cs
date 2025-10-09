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
    private readonly Exception _ex = new InvalidOperationException("Test exception");

    // ----------------------------------------------------------------
    // INFORMATION
    // ----------------------------------------------------------------
    [Benchmark(Baseline = true)]
    public void ILogger_LogInformation() =>
        _logger.LogInformation("User {UserId} logged in", 42);

    [Benchmark]
    public void ExLogger_LogInformation() =>
        ExLogger.ExLogInformation(_logger, "User {UserId} logged in", 42);

    // ----------------------------------------------------------------
    // WARNING
    // ----------------------------------------------------------------
    [Benchmark]
    public void ILogger_LogWarning() =>
        _logger.LogWarning("Low disk space on {Drive}", "C:");

    [Benchmark]
    public void ExLogger_LogWarning() =>
        ExLogger.ExLogWarning(_logger, "Low disk space on {Drive}", "C:");

    // ----------------------------------------------------------------
    // ERROR (with exception)
    // ----------------------------------------------------------------
    [Benchmark]
    public void ILogger_LogError() =>
        _logger.LogError(_ex, "Error for user {UserId}", 42);

    [Benchmark]
    public void ExLogger_LogError() =>
        ExLogger.ExLogError(_logger, "Error for user {UserId}", _ex, 42);

    // ----------------------------------------------------------------
    // CRITICAL (with exception)
    // ----------------------------------------------------------------
    [Benchmark]
    public void ILogger_LogCritical() =>
        _logger.LogCritical(_ex, "Critical failure {OpId}", Guid.NewGuid());

    [Benchmark]
    public void ExLogger_LogCritical() =>
        ExLogger.ExLogCritical(_logger, "Critical failure {OpId}", _ex, Guid.NewGuid());

    // ----------------------------------------------------------------
    // TRACE (cheap, no arguments)
    // ----------------------------------------------------------------
    [Benchmark]
    public void ILogger_LogTrace() =>
        _logger.LogTrace("Trace message");

    [Benchmark]
    public void ExLogger_LogTrace() =>
        ExLogger.ExLogTrace(_logger, "Trace message");

    // ----------------------------------------------------------------
    // HIGH-THROUGHPUT LOOP (BatchLogger batching efficiency)
    // ----------------------------------------------------------------
    [Benchmark]
    public void ILogger_HighThroughput()
    {
        for (int i = 0; i < 1000; i++)
        {
            _logger.LogInformation("Raw looped log {Index}", i);
        }
    }

    [Benchmark]
    public void ExLogger_HighThroughput()
    {
        for (int i = 0; i < 1000; i++)
        {
            ExLogger.ExLogInformation(_logger, "ExLogger looped log {Index}", i);
        }
    }
}