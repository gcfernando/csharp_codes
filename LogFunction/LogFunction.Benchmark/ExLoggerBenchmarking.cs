using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using LogFunction.Logger;
using Microsoft.Extensions.Logging;

namespace LogFunction.Benchmark;

[MemoryDiagnoser] // Allocations: B/op & allocs/op
[GcForce]         // Force GC between iterations for consistent memory results
[GcServer(true)]  // Use Server GC (realistic for ASP.NET, background services)
[RankColumn]       // Add performance ranking
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns("Error", "Median")] // Cleaner output
[HardwareCounters(
    HardwareCounter.TotalCycles,
    HardwareCounter.InstructionRetired,
    HardwareCounter.CacheMisses,
    HardwareCounter.BranchMispredictions)]
public class ExLoggerBenchmarks
{
    private readonly ILogger<ExLoggerBenchmarks> _logger;
    private readonly Dictionary<string, object> _context;
    private readonly Exception _sampleException;

    public ExLoggerBenchmarks()
    {
        _logger = LoggerFactory.Create(builder =>
            builder.AddConsole()).CreateLogger<ExLoggerBenchmarks>();

        _context = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid(),
            ["UserId"] = 12345,
            ["TransactionId"] = Guid.NewGuid()
        };

        _sampleException = new InvalidOperationException("Benchmark test exception",
            new Exception("Simulated inner exception"));
    }

    // -------------------------------------------------------------
    // 1. Baseline (ILogger Direct)
    // -------------------------------------------------------------
    [Benchmark(Baseline = true)]
    public void Baseline_ILogger_LogInformation_NoArgs() =>
        _logger.LogInformation("Baseline ILogger info log.");

    [Benchmark]
    public void Baseline_ILogger_LogInformation_WithArgs() =>
        _logger.LogInformation("Baseline structured: User {UserId} at {UtcNow}", 12345, DateTime.UtcNow);

    [Benchmark]
    public void Baseline_ILogger_LogError_WithException() =>
        _logger.LogError(_sampleException, "Baseline exception logging with {Context}", "ILogger");

    // -------------------------------------------------------------
    // 2. ExLogger Fast-Path Logging (No Args)
    // -------------------------------------------------------------
    [Benchmark] public void ExLogger_LogTrace_NoArgs() => ExLogger.LogTrace(_logger, "ExLogger trace log.");
    [Benchmark] public void ExLogger_LogDebug_NoArgs() => ExLogger.LogDebug(_logger, "ExLogger debug log.");
    [Benchmark] public void ExLogger_LogInformation_NoArgs() => ExLogger.LogInformation(_logger, "ExLogger info log.");
    [Benchmark] public void ExLogger_LogWarning_NoArgs() => ExLogger.LogWarning(_logger, "ExLogger warning log.");
    [Benchmark] public void ExLogger_LogError_NoArgs() => ExLogger.LogError(_logger, "ExLogger error log.");
    [Benchmark] public void ExLogger_LogCritical_NoArgs() => ExLogger.LogCritical(_logger, "ExLogger critical log.", null);

    // -------------------------------------------------------------
    // 3. Structured Logging (Fallback Path)
    // -------------------------------------------------------------
    [Benchmark]
    public void ExLogger_LogInformation_WithArgs() =>
        ExLogger.LogInformation(_logger, "User {UserId} performed {Action} at {Time}", 12345, "Login", DateTime.UtcNow);

    [Benchmark]
    public void ExLogger_LogWarning_WithNumberedArgs() =>
        ExLogger.LogWarning(_logger, "Warning executed at {0}", DateTime.UtcNow);

    // -------------------------------------------------------------
    // 4. Exception Logging
    // -------------------------------------------------------------
    [Benchmark] public void ExLogger_LogErrorException() => ExLogger.LogErrorException(_logger, _sampleException, "Error during operation");
    [Benchmark] public void ExLogger_LogCriticalException() => ExLogger.LogCriticalException(_logger, _sampleException, "Critical operation failure");

    [Benchmark]
    public void ExLogger_LogExceptionWithFormatter()
    {
        ExLogger.LogExceptionWithFormatter(
            _logger,
            _sampleException,
            LogLevel.Error,
            (ex, title, _) => $"[CUSTOM FORMAT] {title}: {ex.Message}",
            "Custom formatted exception");
    }

    // -------------------------------------------------------------
    // 5. Generic Log Method
    // -------------------------------------------------------------
    [Benchmark] public void ExLogger_GenericLog_NoArgs() => ExLogger.Log(_logger, LogLevel.Information, "Generic log without args");
    [Benchmark] public void ExLogger_GenericLog_WithArgs() => ExLogger.Log(_logger, LogLevel.Debug, "Generic structured log with {Id}", Guid.NewGuid());

    // -------------------------------------------------------------
    // 6. Scopes
    // -------------------------------------------------------------
    [Benchmark]
    public void ExLogger_BeginScope_SingleKey()
    {
        using (ExLogger.BeginScope(_logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(_logger, "Inside single-key scope.");
        }
    }

    [Benchmark]
    public void ExLogger_BeginScope_MultiKey()
    {
        using (ExLogger.BeginScope(_logger, _context))
        {
            ExLogger.LogInformation(_logger, "Inside multi-key scope.");
        }
    }

    // -------------------------------------------------------------
    // 7. Async Scope Logging
    // -------------------------------------------------------------
    [Benchmark]
    public async Task ExLogger_LogInsideAsyncScope()
    {
        using (ExLogger.BeginScope(_logger, "AsyncRequestId", Guid.NewGuid()))
        {
            await Task.Delay(1);
            ExLogger.LogDebug(_logger, "Logging inside async scope.");
        }
    }

    // -------------------------------------------------------------
    // 8. High-Volume Throughput (1,000 logs in loop)
    // -------------------------------------------------------------
    [Benchmark]
    public void ExLogger_HighVolume_Throughput()
    {
        for (int i = 0; i < 1000; i++)
        {
            ExLogger.LogInformation(_logger, "High-volume test log {Index}", i);
        }
    }
}
