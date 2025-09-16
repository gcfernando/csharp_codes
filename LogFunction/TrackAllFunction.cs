using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction;

public class TrackAllFunction
{
    private readonly ILogger<TrackAllFunction> _logger;

    public TrackAllFunction(ILogger<TrackAllFunction> logger) =>
        _logger = logger;

    [Function("track-all-function")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ------------------------------
        // 1. Basic Logging (No Arguments)
        // ------------------------------
        ExLogger.LogTrace(_logger, "This is a trace message from ExLogger.");
        ExLogger.LogDebug(_logger, "This is a debug message from ExLogger.");
        ExLogger.LogInformation(_logger, "This is an informational message from ExLogger.");
        ExLogger.LogWarning(_logger, "This is a warning message from ExLogger.");
        ExLogger.LogError(_logger, "This is an error message from ExLogger.");
        ExLogger.LogCritical(_logger, "This is a critical message from ExLogger.", null);

        // ----------------------------------------
        // 2. Logging with Structured Arguments
        // ----------------------------------------
        // Named arguments
        ExLogger.LogInformation(_logger, "This is an informational message executed at {dateTime}.", DateTime.UtcNow);

        // Numbered arguments
        ExLogger.LogWarning(_logger, "This is a warning message executed at {0}.", DateTime.UtcNow);

        // Multiple arguments
        ExLogger.LogDebug(_logger, "User {UserId} performed {Action} at {Time}.", 12345, "Login", DateTime.UtcNow);

        // -------------------------------
        // 3. Generic Log Method
        // -------------------------------
        // Without arguments
        ExLogger.Log(_logger, LogLevel.Warning, "This is a generic log method without arguments.");

        // With arguments
        ExLogger.Log(_logger, LogLevel.Error, "This is a generic log method with arguments executed at {dateTime}.", DateTime.UtcNow);

        // --------------------------------
        // 4. Exception Logging
        // --------------------------------
        try
        {
            const int counter = 5;
            _ = counter / int.Parse("0"); // Will throw DivideByZeroException
        }
        catch (Exception ex)
        {
            ExLogger.LogException(_logger, ex); // Structured exception logging
        }

        // -------------------------------
        // 5. Log Scopes (Contextual Logging)
        // -------------------------------
        // Single key-value pair
        using (ExLogger.BeginScope(_logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(_logger, "Processing request within a scoped context.");
            ExLogger.LogWarning(_logger, "Scoped warning example.");
        }

        // Multiple key-value pairs
        var context = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid(),
            ["UserId"] = 12345,
            ["TransactionId"] = Guid.NewGuid()
        };

        using (ExLogger.BeginScope(_logger, context))
        {
            ExLogger.LogInformation(_logger, "Processing request with multiple contextual values.");
            ExLogger.LogError(_logger, "Scoped error example.");
        }

        // -------------------------------
        // 6. Mixed Example
        // -------------------------------
        try
        {
            // Simulate another exception
            _ = int.Parse("NotANumber");
        }
        catch (Exception ex)
        {
            using (ExLogger.BeginScope(_logger, "RequestId", Guid.NewGuid()))
            {
                ExLogger.LogException(_logger, ex);
                ExLogger.LogCritical(_logger, "Critical failure inside scoped context.", ex);
            }
        }

        return new OkObjectResult("Logger executed successfully with all features.");
    }
}