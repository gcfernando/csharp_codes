using LogFunction.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction.Core;

public class TrackAllFunction(ILogger<TrackAllFunction> logger)
{
    [Function("track-all-function")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ----------------------------------------------------------------
        // 1. Basic Logging (No Arguments, Convenience Methods)
        // ----------------------------------------------------------------
        ExLogger.LogTrace(logger, "This is a trace log from ExLogger.");
        ExLogger.LogDebug(logger, "This is a debug log from ExLogger.");
        ExLogger.LogInformation(logger, "This is an informational log from ExLogger.");
        ExLogger.LogWarning(logger, "This is a warning log from ExLogger.");
        ExLogger.LogError(logger, "This is an error log from ExLogger.");
        ExLogger.LogCritical(logger, "This is a critical log from ExLogger.", null);

        // ----------------------------------------------------------------
        // 2. Structured Logging (Arguments)
        // ----------------------------------------------------------------
        ExLogger.LogInformation(logger, "Structured log executed at {DateTime}.", DateTime.UtcNow);
        ExLogger.LogWarning(logger, "Warning with numbered placeholder {0}.", DateTime.UtcNow);
        ExLogger.LogDebug(logger, "User {UserId} performed {Action} at {Time}.", 12345, "Login", DateTime.UtcNow);

        // ----------------------------------------------------------------
        // 3. Generic Log Method
        // ----------------------------------------------------------------
        ExLogger.Log(logger, LogLevel.Warning, "Generic log without arguments.");
        ExLogger.Log(logger, LogLevel.Error, "Generic log executed at {UtcNow}.", DateTime.UtcNow);

        // ----------------------------------------------------------------
        // 4. Exception Logging
        // ----------------------------------------------------------------
        try
        {
            const int counter = 5;
            _ = counter / int.Parse("0"); // Throws DivideByZeroException
        }
        catch (Exception ex)
        {
            ExLogger.LogException(logger, ex);
            ExLogger.LogError(logger, "Handled an exception with context.", ex);
        }

        // ----------------------------------------------------------------
        // 5. Log Scopes (Contextual Logging)
        // ----------------------------------------------------------------
        // Single key-value scope
        using (ExLogger.BeginScope(logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(logger, "Processing inside single key-value scope.");
            ExLogger.LogWarning(logger, "Scoped warning example.");
        }

        // Multiple key-value pairs
        var context = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid(),
            ["UserId"] = 98765,
            ["TransactionId"] = Guid.NewGuid()
        };

        using (ExLogger.BeginScope(logger, context))
        {
            ExLogger.LogInformation(logger, "Processing with multiple contextual values.");
            ExLogger.LogError(logger, "Error inside multi-key scope.");
        }

        // ----------------------------------------------------------------
        // 6. Mixed Example: Critical Failure in Scope
        // ----------------------------------------------------------------
        try
        {
            _ = int.Parse("NotANumber"); // Will throw FormatException
        }
        catch (Exception ex)
        {
            using (ExLogger.BeginScope(logger, "RequestId", Guid.NewGuid()))
            {
                ExLogger.LogException(logger, ex);
                ExLogger.LogCritical(logger, "Critical failure inside scoped context.", ex);
            }
        }

        // ----------------------------------------------------------------
        // 7. High Throughput Example (simulated heavy logging)
        // ----------------------------------------------------------------
        for (var i = 0; i < 1000; i++) // simulate 1000 logs in quick succession
        {
            ExLogger.LogDebug(logger, "High-throughput log #{LogIndex} at {UtcNow}.", i, DateTime.UtcNow);
        }

        return new OkObjectResult("Logger executed successfully with all features.");
    }
}