using LogFunction.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction.Core;

/// <summary>
/// Demonstrates **all features** of <see cref="ExLogger"/> and <see cref="BatchLogger"/>.
/// <para>
/// Includes:
/// <list type="bullet">
///   <item>Fast-path delegate logging (allocation-free)</item>
///   <item>Structured messages with placeholders</item>
///   <item>Generic overloads and exception handling</item>
///   <item>Scoped logging (single and multiple keys)</item>
///   <item>Custom exception formatting</item>
///   <item>Batch logging with async flush</item>
///   <item>Throughput stress demonstration</item>
/// </list>
/// </para>
/// <para>
/// ⚙️ <b>Note:</b> In production, register <see cref="BatchLogger"/> as a Singleton in DI.
/// This ensures only one background drain task across all Azure Function instances.
/// </para>
/// </summary>
public class TrackAllFunction(ILogger<TrackAllFunction> logger, BatchLogger batchLogger)
{
    [Function("logger-test")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ===============================================================
        // SECTION A: ExLogger — Direct / Immediate Logging
        // ===============================================================
        DemoExLogger(logger);

        // ===============================================================
        // SECTION B: BatchLogger — Buffered / Asynchronous Logging
        // ===============================================================
        DemoBatchLogger(batchLogger);

        // ===============================================================
        // SECTION C: Throughput Stress Test
        // ===============================================================
        for (var i = 0; i < 20; i++)
        {
            batchLogger.LogInformation(
                "Throughput test log #{Index} at {UtcNow}",
                null,
                i,
                DateTime.UtcNow
            );
        }

        // Explicit flush (important for short-lived Function executions)
        await batchLogger.FlushAsync();

        // ===============================================================
        // SECTION D: Return HTTP Response
        // ===============================================================
        return new OkObjectResult(new
        {
            Message = "Logger demo completed. Check console or Application Insights logs.",
            Timestamp = DateTime.UtcNow
        });
    }

    // -----------------------------------------------------------------
    // 🧩 ExLogger: Demonstration of All Logging Capabilities
    // -----------------------------------------------------------------
    private static void DemoExLogger(ILogger logger)
    {
        // --- 1️⃣ Fast-path (predefined delegates for zero allocation) ---
        logger.ExLogTrace("ExLogger Trace message");
        logger.ExLogDebug("ExLogger Debug message");
        logger.ExLogInformation("ExLogger Information message");
        logger.ExLogWarning("ExLogger Warning message");
        logger.ExLogError("ExLogger Error message");
        logger.ExLogCritical("ExLogger Critical message");

        // --- 2️⃣ Structured Logging ---
        logger.ExLogInformation("Structured info at {UtcNow}", DateTime.UtcNow);
        logger.ExLogDebug("User {UserId} performed {Action}", 42, "Login");

        // --- 3️⃣ Generic overloads ---
        ExLogger.Log(logger, LogLevel.Information, "Generic info via Log() helper");
        ExLogger.Log(logger, LogLevel.Warning, "Generic warning with Guid {Id}", Guid.NewGuid());

        // --- 4️⃣ Exception Handling ---
        try
        {
            throw new InvalidOperationException("Sample ExLogger exception");
        }
        catch (Exception ex)
        {
            // Automatic formatting (default formatter)
            logger.ExLogErrorException(ex, "Handled exception via ExLogger");
            logger.ExLogCriticalException(ex, "Critical exception via ExLogger");

            // Structured message template + exception
            ExLogger.Log(logger, LogLevel.Error, ex, "Structured exception at {Now}", DateTime.UtcNow);
        }

        // --- 5️⃣ Scoped Logging (adds contextual metadata) ---
        using (logger.ExBeginScope("RequestId", Guid.NewGuid()))
        {
            logger.ExLogInformation("Inside single-key ExLogger scope");
        }

        var ctx = new Dictionary<string, object>
        {
            ["UserId"] = 101,
            ["TransactionId"] = Guid.NewGuid()
        };

        using (logger.ExBeginScope(ctx))
        {
            logger.ExLogWarning("Inside multi-key ExLogger scope");
        }
    }

    // -----------------------------------------------------------------
    // ⚡ BatchLogger: Buffered, Asynchronous Logging Demo
    // -----------------------------------------------------------------
    private static void DemoBatchLogger(BatchLogger batchLogger)
    {
        // --- 1️⃣ Basic Logging ---
        batchLogger.LogTrace("BatchLogger Trace message");
        batchLogger.LogDebug("BatchLogger Debug message");
        batchLogger.LogInformation("BatchLogger Information message");
        batchLogger.LogWarning("BatchLogger Warning message");
        batchLogger.LogError("BatchLogger Error message");
        batchLogger.LogCritical("BatchLogger Critical message");

        // --- 2️⃣ Structured Logging ---
        batchLogger.LogInformation("Structured batch log {UtcNow}", null, DateTime.UtcNow);
        batchLogger.LogDebug("Batch user {UserId} executed {Action}", null, 7, "Checkout");

        // --- 3️⃣ Exception Logging ---
        try
        {
            throw new ArgumentNullException(nameof(batchLogger), "Simulated BatchLogger exception");
        }
        catch (Exception ex)
        {
            batchLogger.LogError("Batch error with exception {Code}", ex, "ERR-001");
            batchLogger.LogCritical("Batch critical failure {OpId}", ex, Guid.NewGuid());

            // Formatted exceptions
            batchLogger.LogErrorException(ex, "Batch Error (formatter demo)");
            batchLogger.LogCriticalException(ex, "Batch Critical (formatter demo)");
        }

        // --- 4️⃣ Scoped Logging ---
        using (batchLogger.BeginScope("BatchScopeId", Guid.NewGuid()))
        {
            batchLogger.LogInformation("Inside single-key BatchLogger scope");
        }

        var ctx = new Dictionary<string, object>
        {
            ["BatchUserId"] = 202,
            ["BatchTxnId"] = Guid.NewGuid()
        };

        using (batchLogger.BeginScope(ctx))
        {
            batchLogger.LogWarning("Inside multi-key BatchLogger scope");
        }
    }
}
