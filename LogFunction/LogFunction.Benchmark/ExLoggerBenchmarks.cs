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
public class ExLoggerAllMethodsBenchmarks
{
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly Exception _ex = new InvalidOperationException("Something went wrong");

    // ---- Generic ----
    [Benchmark]
    public void Log_Generic_Message() =>
        ExLogger.Log(_logger, LogLevel.Information, "Generic message");

    [Benchmark]
    public void Log_Generic_Message_Exception() =>
        ExLogger.Log(_logger, LogLevel.Warning, "Warn with exception", _ex);

    [Benchmark]
    public void Log_Generic_Template_Exception() =>
        ExLogger.Log(_logger, LogLevel.Error, _ex, "Order {OrderId} failed", 123);

    // ---- Convenience ----
    [Benchmark] public void LogTrace() => ExLogger.LogTrace(_logger, "Trace message");

    [Benchmark] public void LogDebug() => ExLogger.LogDebug(_logger, "Debug {X}", 42);

    [Benchmark] public void LogInformation() => ExLogger.LogInformation(_logger, "Info {EventId}", 77);

    [Benchmark] public void LogWarning() => ExLogger.LogWarning(_logger, "Warn {Code}", "W123");

    [Benchmark] public void LogError_WithException() => ExLogger.LogError(_logger, "Error {User}", _ex, "user1");

    [Benchmark] public void LogError_NoException() => ExLogger.LogError(_logger, "Error no exception");

    [Benchmark] public void LogCritical_WithException() => ExLogger.LogCritical(_logger, "Critical failure", _ex);

    // ---- Exception Helpers ----
    [Benchmark]
    public void LogErrorException() =>
        ExLogger.LogErrorException(_logger, _ex, "System Error", moreDetailsEnabled: true);

    [Benchmark]
    public void LogCriticalException() =>
        ExLogger.LogCriticalException(_logger, _ex, "Fatal Error", moreDetailsEnabled: true);

    // ---- Scopes ----
    [Benchmark]
    public void BeginScope_Single() =>
        ExLogger.BeginScope(_logger, "RequestId", Guid.NewGuid()).Dispose();

    [Benchmark]
    public void BeginScope_SmallDictionary()
    {
        var ctx = new Dictionary<string, object> { { "UserId", 1 }, { "OrderId", 42 } };
        ExLogger.BeginScope(_logger, ctx).Dispose();
    }

    [Benchmark]
    public void BeginScope_LargeDictionary()
    {
        var ctx = Enumerable.Range(0, 10).ToDictionary(i => "Key" + i, i => (object)i);
        ExLogger.BeginScope(_logger, ctx).Dispose();
    }
}