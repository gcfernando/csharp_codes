using System.Text.Json;
using LogFunction.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction.Core;

/// <summary>
/// Azure Function that exercises the full ExLogger surface:
/// - Fast-path delegates (no-arg logs)
/// - Structured logging with {Placeholders}
/// - Generic Log overloads (with/without exception)
/// - Exception logging at Error/Critical with global or per-call formatter
/// - Scoped logging (single & multi key)
/// - Async scope example
/// - AggregateException to demonstrate recursive inner-exception formatting
/// </summary>
public class ExLoggerTestFunction(ILogger<ExLoggerTestFunction> logger)
{
    [Function("exlogger-test")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ----------------------------------------------------------------
        // 0) (Optional) Demonstrate GLOBAL custom formatter for exceptions
        //     NOTE: We restore the original formatter after we use it.
        // ----------------------------------------------------------------
        var originalFormatter = ExLogger.ExceptionFormatter;
        ExLogger.ExceptionFormatter = (ex, title, details) =>
        {
            var payload = new
            {
                title,
                type = ex.GetType().FullName,
                message = ex.Message,
                hresult = ex.HResult,
                source = ex.Source,
                target = ex.TargetSite?.Name,
                inner = ex.InnerException is not null,
                includeDetails = details
            };

            return System.Text.Json.JsonSerializer.Serialize(payload);
        };

        // ----------------------------------------------------------------
        // 1) Fast-path delegates (no args) -> minimal allocations
        // ----------------------------------------------------------------
        ExLogger.LogTrace(logger, "Trace log from ExLogger (fast path).");
        ExLogger.LogDebug(logger, "Debug log from ExLogger (fast path).");
        ExLogger.LogInformation(logger, "Information log from ExLogger (fast path).");
        ExLogger.LogWarning(logger, "Warning log from ExLogger (fast path).");
        ExLogger.LogError(logger, "Error log from ExLogger (fast path).");
        ExLogger.LogCritical(logger, "Critical log from ExLogger (fast path).", exception: null);

        // ----------------------------------------------------------------
        // 2) Structured logging ({Placeholders}) -> structured/fallback path
        // ----------------------------------------------------------------
        ExLogger.LogInformation(logger, "Structured info at {UtcNow}", DateTime.UtcNow);
        ExLogger.LogWarning(logger, "Numbered placeholder {0}", 42);
        ExLogger.LogDebug(logger, "User {UserId} performed {Action} at {UtcNow}", 12345, "Login", DateTime.UtcNow);

        // Also show the generic overload with explicit exception + template
        var demoEx = new InvalidOperationException("Demo for structured overload");
        ExLogger.Log(logger, LogLevel.Warning, demoEx, "Structured overload used for {Area}", "DemoArea");

        // ----------------------------------------------------------------
        // 3) Generic Log method family
        // ----------------------------------------------------------------
        ExLogger.Log(logger, LogLevel.Trace, "Generic Trace message.");
        ExLogger.Log(logger, LogLevel.Debug, "Generic Debug with {Id}", Guid.NewGuid());
        ExLogger.Log(logger, LogLevel.Information, "Generic Info at {UtcNow}", DateTime.UtcNow);
        ExLogger.Log(logger, LogLevel.Warning, "Generic Warning message.");
        ExLogger.Log(logger, LogLevel.Error, "Generic Error with arg {Arg}", "arg-value");
        ExLogger.Log(logger, LogLevel.Critical, "Generic Critical message!");

        // ----------------------------------------------------------------
        // 4) Exception logging (GLOBAL JSON formatter currently active)
        // ----------------------------------------------------------------
        try
        {
            var x = 0;
            _ = 10 / x;
        }
        catch (Exception ex)
        {
            // Will use the GLOBAL JSON formatter
            ExLogger.LogErrorException(logger, ex, "Divide-by-zero detected");
            // Structured message with attached exception (template path)
            ExLogger.Log(logger, LogLevel.Error, ex, "Handled divide-by-zero at {UtcNow}", DateTime.UtcNow);
        }

        // ----------------------------------------------------------------
        // 5) Switch back to DEFAULT formatter and show per-call formatter
        // ----------------------------------------------------------------
        ExLogger.ExceptionFormatter = originalFormatter;

        try
        {
            // Force AggregateException to exercise recursive inner exceptions
            try
            {
                await Task.WhenAll(
                    Task.Run(() => throw new FormatException("Inner format broke")),
                    Task.Run(() => throw new TimeoutException("Inner timeout"))
                );
            }
            catch (Exception inner)
            {
                throw new AggregateException("Outer aggregate failure", inner);
            }
        }
        catch (Exception ex)
        {
            // (a) Use DEFAULT formatter via LogCriticalException
            ExLogger.LogCriticalException(logger, ex, "Aggregate processing failure (default formatting)");

            // (b) PER-CALL custom formatter for this one log only
            ExLogger.LogExceptionWithFormatter(
                logger,
                ex,
                LogLevel.Error,
                (e, title, includeDetails) =>
                {
                    var lines = new List<string>
                    {
                        $"[{DateTime.UtcNow:O}] {title}",
                        $"Type: {e.GetType().FullName}",
                        $"Message: {e.Message}"
                    };
                    if (includeDetails && !string.IsNullOrWhiteSpace(e.StackTrace))
                    {
                        lines.Add("StackTrace:");
                        lines.Add(e.StackTrace);
                    }
                    return string.Join(Environment.NewLine, lines);
                },
                title: "Aggregate failure (per-call custom formatting)",
                moreDetailsEnabled: true
            );
        }

        // ----------------------------------------------------------------
        // 6) Scopes (single & multi key)
        // ----------------------------------------------------------------
        using (ExLogger.BeginScope(logger, "RequestId", Guid.NewGuid()))
        {
            ExLogger.LogInformation(logger, "Inside single-key scope at {UtcNow}", DateTime.UtcNow);
        }

        var multi = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid(),
            ["UserId"] = 777,
            ["TransactionId"] = Guid.NewGuid()
        };

        using (ExLogger.BeginScope(logger, multi))
        {
            ExLogger.LogInformation(logger, "Inside multi-key scope");
            ExLogger.LogError(logger, "Error within multi-key scope for {TransactionId}", multi["TransactionId"]);
        }

        // ----------------------------------------------------------------
        // 7) Mixed Example: NullReferenceException + both Error/Critical detailed logs
        // ----------------------------------------------------------------
        try
        {
            string? s = null;
            _ = s!.Length;
        }
        catch (Exception ex)
        {
            using (ExLogger.BeginScope(logger, "Operation", "ScopedCriticalTest"))
            {
                ExLogger.LogErrorException(logger, ex, "Failure in scoped operation");
                ExLogger.LogCriticalException(logger, ex, "Scoped critical error occurred");
            }
        }

        // ----------------------------------------------------------------
        // 8) Async scope + throughput mini-demo
        // ----------------------------------------------------------------
        using (ExLogger.BeginScope(logger, "AsyncRequestId", Guid.NewGuid()))
        {
            for (var i = 0; i < 5; i++)
            {
                ExLogger.LogDebug(logger, "High-throughput log {Index} at {UtcNow}", i, DateTime.UtcNow);
                await Task.Delay(10);
            }
        }

        // Final fast-path log to show delegates again
        ExLogger.LogInformation(logger, "ExLogger test completed successfully.");

        // ----------------------------------------------------------------
        // 9) HTTP response
        // ----------------------------------------------------------------
        return new OkObjectResult(new
        {
            Message = "ExLogger test executed with latest features",
            Timestamp = DateTime.UtcNow
        });
    }
}