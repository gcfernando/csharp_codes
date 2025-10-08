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
    [Benchmark] public void LogTrace() => ExLogger.ExLogTrace(_logger, "Trace message");

    [Benchmark] public void LogDebug() => ExLogger.ExLogDebug(_logger, "Debug {X}", 42);

    [Benchmark] public void LogInformation() => ExLogger.ExLogInformation(_logger, "Info {EventId}", 77);

    [Benchmark] public void LogWarning() => ExLogger.ExLogWarning(_logger, "Warn {Code}", "W123");

    [Benchmark] public void LogError_WithException() => ExLogger.ExLogError(_logger, "Error {User}", _ex, "user1");

    [Benchmark] public void LogError_NoException() => ExLogger.ExLogError(_logger, "Error no exception");

    [Benchmark] public void LogCritical_WithException() => ExLogger.ExLogCritical(_logger, "Critical failure", _ex);

    // ---- Exception Helpers ----
    [Benchmark]
    public void LogErrorException() =>
        ExLogger.ExLogErrorException(_logger, _ex, "System Error", moreDetailsEnabled: true);

    [Benchmark]
    public void LogCriticalException() =>
        ExLogger.ExLogCriticalException(_logger, _ex, "Fatal Error", moreDetailsEnabled: true);

    // ---- Scopes ----
    [Benchmark]
    public void BeginScope_Single() =>
        ExLogger.ExBeginScope(_logger, "RequestId", Guid.NewGuid()).Dispose();

    [Benchmark]
    public void BeginScope_SmallDictionary()
    {
        var ctx = new Dictionary<string, object> { { "UserId", 1 }, { "OrderId", 42 } };
        ExLogger.ExBeginScope(_logger, ctx).Dispose();
    }

    [Benchmark]
    public void BeginScope_LargeDictionary()
    {
        var ctx = Enumerable.Range(0, 10).ToDictionary(i => "Key" + i, i => (object)i);
        ExLogger.ExBeginScope(_logger, ctx).Dispose();
    }
}