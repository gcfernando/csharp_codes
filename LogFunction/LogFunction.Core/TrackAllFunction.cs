using LogFunction.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction.Core;

/// <summary>
/// Azure Function demonstrating **all possible usages** of <see cref="ExLogger"/> and <see cref="BatchLogger"/>.
/// <para>
/// Covers:
/// <list type="bullet">
///   <item>Fast-path delegates (no-arg logs)</item>
///   <item>Structured logging with {Placeholders}</item>
///   <item>Generic Log overloads (with/without exception)</item>
///   <item>Exception logging (default + structured + critical)</item>
///   <item>Scoped logging (single & multi key scopes)</item>
///   <item>Async flush demo</item>
///   <item>Throughput stress loop</item>
///   <item>Comparison: ExLogger vs BatchLogger</item>
/// </list>
/// </para>
/// <para>
/// ⚠️ NOTE:
/// * In this demo we use "using var" for <see cref="BatchLogger"/> to ensure cleanup per request.
/// * In production you would typically register <see cref="BatchLogger"/> as a Singleton via DI.
/// </para>
/// </summary>
public class TrackAllFunction(ILogger<TrackAllFunction> logger)
{
    [Function("logger-test")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ====================================================================
        // SECTION A: ExLogger Demo
        // ====================================================================
        DemoExLogger(logger);

        // ====================================================================
        // SECTION B: BatchLogger Demo
        // ====================================================================
        using var batchLogger = new BatchLogger(
            logger,
            capacity: 5000,                  // max buffered messages
            batchSize: 100,                  // flush when 100 reached
            flushInterval: TimeSpan.FromMilliseconds(200)); // or flush on interval

        DemoBatchLogger(batchLogger);

        // ====================================================================
        // SECTION C: Mini stress test (BatchLogger throughput)
        // ====================================================================
        for (var i = 0; i < 20; i++)
        {
            // 🚀 Enqueue 20 logs quickly (non-blocking)
            batchLogger.LogInformation("High-throughput batching log {Index} at {UtcNow}", null, i, DateTime.UtcNow);
        }

        // Explicit flush ensures logs are written before HTTP response (important in Functions!)
        await batchLogger.FlushAsync();

        // ====================================================================
        // Final HTTP response
        // ====================================================================
        return new OkObjectResult(new
        {
            Message = "Logger demo executed. Check logs for ExLogger and BatchLogger usage.",
            Timestamp = DateTime.UtcNow
        });
    }

    // ============================================================
    // ExLogger demo
    // ============================================================
    private static void DemoExLogger(ILogger logger)
    {
        // ---- Fast-path delegates (precompiled for performance) ----
        ExLogger.LogTrace(logger, "ExLogger Trace");
        ExLogger.LogDebug(logger, "ExLogger Debug");
        ExLogger.LogInformation(logger, "ExLogger Info");
        ExLogger.LogWarning(logger, "ExLogger Warning");
        ExLogger.LogError(logger, "ExLogger Error");
        ExLogger.LogCritical(logger, "ExLogger Critical", exception: null);

        // ---- Structured logs ----
        ExLogger.LogInformation(logger, "ExLogger structured info {UtcNow}", DateTime.UtcNow);
        ExLogger.LogDebug(logger, "ExLogger User {UserId} performed {Action}", 42, "Login");

        // ---- Generic overloads ----
        ExLogger.Log(logger, LogLevel.Information, "Generic info message");
        ExLogger.Log(logger, LogLevel.Warning, "Generic warning with {Id}", Guid.NewGuid());

        // ---- Exception logging ----
        try
        {
            throw new InvalidOperationException("ExLogger demo exception");
        }
        catch (Exception ex)
        {
            // Error & Critical exception logging
            ExLogger.LogErrorException(logger, ex, "Error logged with default formatter");
            ExLogger.LogCriticalException(logger, ex, "Critical error with default formatter");

            // Structured template overload with exception
            ExLogger.Log(logger, LogLevel.Error, ex, "Structured exception with {Timestamp}", DateTime.UtcNow);
        }

        // ---- Scoped logging (adds context to logs) ----
        using (ExLogger.BeginScope(logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(logger, "Inside single-key ExLogger scope");
        }

        var ctx = new Dictionary<string, object>
        {
            ["UserId"] = 101,
            ["TransactionId"] = Guid.NewGuid()
        };

        using (ExLogger.BeginScope(logger, ctx))
        {
            ExLogger.LogWarning(logger, "Inside multi-key ExLogger scope");
        }
    }

    // ============================================================
    // BatchLogger demo
    // ============================================================
    private static void DemoBatchLogger(BatchLogger batchingLogger)
    {
        // ---- Basic logs ----
        batchingLogger.LogTrace("BatchLogger Trace");
        batchingLogger.LogDebug("BatchLogger Debug");
        batchingLogger.LogInformation("BatchLogger Info");
        batchingLogger.LogWarning("BatchLogger Warning");
        batchingLogger.LogError("BatchLogger Error");
        batchingLogger.LogCritical("BatchLogger Critical");

        // ---- Structured logs ----
        batchingLogger.LogInformation("Batch info {UtcNow}", null, DateTime.UtcNow);
        batchingLogger.LogDebug("Batch user {UserId} did {Action}", null, 7, "Checkout");

        // ---- Exception logging ----
        try
        {
            throw new ArgumentNullException(nameof(batchingLogger), "BatchLogger demo exception");
        }
        catch (Exception ex)
        {
            batchingLogger.LogError("BatchLogger error with exception {Code}", ex, "ERR123");
            batchingLogger.LogCritical("BatchLogger critical failure {OpId}", ex, Guid.NewGuid());
            batchingLogger.LogErrorException(ex, "Error (formatter demo)");
            batchingLogger.LogCriticalException(ex, "Critical (formatter demo)");
        }

        // ---- Scoped logging ----
        using (batchingLogger.BeginScope("BatchRequestId", Guid.NewGuid()))
        {
            batchingLogger.LogInformation("Inside single-key BatchLogger scope");
        }

        var ctx = new Dictionary<string, object>
        {
            ["BatchUserId"] = 202,
            ["BatchTxnId"] = Guid.NewGuid()
        };

        using (batchingLogger.BeginScope(ctx))
        {
            batchingLogger.LogWarning("Inside multi-key BatchLogger scope");
        }
    }
}