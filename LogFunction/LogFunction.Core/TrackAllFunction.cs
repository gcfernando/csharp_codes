using LogFunction.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction.Core;

/// <summary>
/// Azure Function that demonstrates all ExLogger features.
/// </summary>
public class ExLoggerTestFunction(ILogger<ExLoggerTestFunction> logger)
{
    [Function("exlogger-test")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ----------------------------------------------------------------
        // 1. Basic Logs (convenience methods, no args)
        // ----------------------------------------------------------------
        ExLogger.LogTrace(logger, "Trace log from ExLogger.");
        ExLogger.LogDebug(logger, "Debug log from ExLogger.");
        ExLogger.LogInformation(logger, "Information log from ExLogger.");
        ExLogger.LogWarning(logger, "Warning log from ExLogger.");
        ExLogger.LogError(logger, "Error log from ExLogger.");
        ExLogger.LogCritical(logger, "Critical log from ExLogger.", null);

        // ----------------------------------------------------------------
        // 2. Structured Logging (placeholders + arguments)
        // ----------------------------------------------------------------
        ExLogger.LogInformation(logger, "Structured log executed at {UtcNow}", DateTime.UtcNow);
        ExLogger.LogWarning(logger, "Warning with numbered placeholder {0}", 42);
        ExLogger.LogDebug(logger, "User {UserId} performed {Action} at {UtcNow}", 12345, "Login", DateTime.UtcNow);

        // ----------------------------------------------------------------
        // 3. Generic Log Method
        // ----------------------------------------------------------------
        ExLogger.Log(logger, LogLevel.Trace, "Generic Trace message.");
        ExLogger.Log(logger, LogLevel.Debug, "Generic Debug with {Id}", Guid.NewGuid());
        ExLogger.Log(logger, LogLevel.Information, "Generic Info message at {UtcNow}", DateTime.UtcNow);
        ExLogger.Log(logger, LogLevel.Warning, "Generic Warning message.");
        ExLogger.Log(logger, LogLevel.Error, "Generic Error message with args: {0}", "arg-value");
        ExLogger.Log(logger, LogLevel.Critical, "Generic Critical message!");

        // ----------------------------------------------------------------
        // 4. Exception Logging
        // ----------------------------------------------------------------
        try
        {
            var x = 0;
            _ = 10 / x; // will throw DivideByZeroException
        }
        catch (Exception ex)
        {
            ExLogger.LogException(logger, ex, "Divide by zero encountered");
            ExLogger.LogError(logger, "Handled divide by zero exception", ex);
        }

        try
        {
            _ = int.Parse("NotANumber"); // FormatException
        }
        catch (Exception ex)
        {
            ExLogger.LogException(logger, ex, "Parsing error", moreDetailsEnabled: true);
            ExLogger.LogCritical(logger, "Critical parsing failure", ex);
        }

        // ----------------------------------------------------------------
        // 5. Scopes (contextual logging)
        // ----------------------------------------------------------------
        using (ExLogger.BeginScope(logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(logger, "Inside single key-value scope");
            ExLogger.LogWarning(logger, "Warning inside single key scope");
        }

        var context = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid(),
            ["UserId"] = 777,
            ["TransactionId"] = Guid.NewGuid()
        };

        using (ExLogger.BeginScope(logger, context))
        {
            ExLogger.LogInformation(logger, "Inside multi-key scope");
            ExLogger.LogError(logger, "Error inside multi-key scope with {TransactionId}", context["TransactionId"]);
        }

        // ----------------------------------------------------------------
        // 6. Mixed Example: Scoped critical failure
        // ----------------------------------------------------------------
        try
        {
            string? nullString = null;
            _ = nullString!.Length; // NullReferenceException
        }
        catch (Exception ex)
        {
            using (ExLogger.BeginScope(logger, "Operation", "ScopedCriticalTest"))
            {
                ExLogger.LogException(logger, ex, "Critical failure in scoped operation");
                ExLogger.LogCritical(logger, "Scoped critical error occurred", ex);
            }
        }

        // ----------------------------------------------------------------
        // 7. Optional: simulate a burst of logs (high throughput)
        // ----------------------------------------------------------------
        for (var i = 0; i < 5; i++) // keep small for demo
        {
            ExLogger.LogDebug(logger, "High-throughput log {Index} at {UtcNow}", i, DateTime.UtcNow);
            await Task.Delay(10); // simulate work
        }

        return new OkObjectResult(new
        {
            Message = "ExLogger test executed successfully",
            Timestamp = DateTime.UtcNow
        });
    }
}