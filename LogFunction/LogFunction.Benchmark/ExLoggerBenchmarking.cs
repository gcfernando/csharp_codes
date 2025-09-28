using BenchmarkDotNet.Attributes;
using LogFunction.Logger;
using Microsoft.Extensions.Logging;

namespace LogFunction.Benchmark;
[MemoryDiagnoser] // Measures memory allocations
[ThreadingDiagnoser] // Measures threading metrics
[RankColumn] // Ranks results
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class LoggerBenchmarks
{
    private readonly ILogger<LoggerBenchmarks> _logger;
    private readonly Dictionary<string, object> _context;

    public LoggerBenchmarks()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LoggerBenchmarks>();

        _context = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid(),
            ["UserId"] = 12345,
            ["TransactionId"] = Guid.NewGuid()
        };
    }

    #region Basic Logging Benchmarks

    [Benchmark]
    public void LogTrace_NoArgs() =>
        ExLogger.LogTrace(_logger, "This is a trace message.");

    [Benchmark]
    public void LogDebug_NoArgs() =>
        ExLogger.LogDebug(_logger, "This is a debug message.");

    [Benchmark]
    public void LogInformation_NoArgs() =>
        ExLogger.LogInformation(_logger, "This is an information message.");

    [Benchmark]
    public void LogWarning_NoArgs() =>
        ExLogger.LogWarning(_logger, "This is a warning message.");

    [Benchmark]
    public void LogError_NoArgs() =>
        ExLogger.LogError(_logger, "This is an error message.");

    [Benchmark]
    public void LogCritical_NoArgs() =>
        ExLogger.LogCritical(_logger, "This is a critical message.", null);

    #endregion

    #region Structured Logging Benchmarks

    [Benchmark]
    public void LogInformation_WithArgs() =>
        ExLogger.LogInformation(_logger, "User {UserId} performed {Action} at {Time}.", 12345, "Login", DateTime.UtcNow);

    [Benchmark]
    public void LogWarning_WithNumberedArgs() =>
        ExLogger.LogWarning(_logger, "This is a warning executed at {0}.", DateTime.UtcNow);

    #endregion

    #region Exception Logging Benchmark

    [Benchmark]
    public void LogExceptionBenchmark()
    {
        try
        {
            int x = 0;
            _ = 1 / x; // Will throw DivideByZeroException
        }
        catch (Exception ex)
        {
            ExLogger.LogException(_logger, ex);
        }
    }

    #endregion

    #region Generic Log Benchmark

    [Benchmark]
    public void GenericLog_NoArgs() =>
        ExLogger.Log(_logger, LogLevel.Warning, "Generic log without arguments.");

    [Benchmark]
    public void GenericLog_WithArgs() =>
        ExLogger.Log(_logger, LogLevel.Error, "Generic log with arguments executed at {Time}.", DateTime.UtcNow);

    #endregion

    #region Logging Scope Benchmarks

    [Benchmark]
    public void BeginScope_SingleKey()
    {
        using (ExLogger.BeginScope(_logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(_logger, "Logging inside single-key scope.");
        }
    }

    [Benchmark]
    public void BeginScope_MultipleKeys()
    {
        using (ExLogger.BeginScope(_logger, _context))
        {
            ExLogger.LogInformation(_logger, "Logging inside multi-key scope.");
        }
    }

    #endregion

    #region Async Logging Benchmark

    [Benchmark]
    public async Task LogInsideAsyncScope()
    {
        using (ExLogger.BeginScope(_logger, "AsyncRequestId", Guid.NewGuid()))
        {
            await Task.Delay(1);
            ExLogger.LogDebug(_logger, "Logging inside async scope.");
        }
    }

    #endregion
}
