using LogFunction.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LogFunction.Core;

public class TrackAllFunction(ILogger<TrackAllFunction> logger)
{
    [Function("logger-test")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        DemoExLogger(logger);

        return new OkObjectResult(new
        {
            Message = "Logger demo completed. Check console or Application Insights logs.",
            Timestamp = DateTime.UtcNow
        });
    }

    private static void DemoExLogger(ILogger logger)
    {
        logger.ExLogTrace("ExLogger Trace message");
        logger.ExLogDebug("ExLogger Debug message");
        logger.ExLogInformation("ExLogger Information message");
        logger.ExLogWarning("ExLogger Warning message");
        logger.ExLogError("ExLogger Error message");
        logger.ExLogCritical("ExLogger Critical message");

        logger.ExLogInformation("Structured info at {UtcNow}", DateTime.UtcNow);
        logger.ExLogDebug("User {UserId} performed {Action}", 42, "Login");

        ExLogger.Log(logger, LogLevel.Information, "Generic info via Log() helper");
        ExLogger.Log(logger, LogLevel.Warning, "Generic warning with Guid {Id}", Guid.NewGuid());

        try
        {
            throw new InvalidOperationException("Sample ExLogger exception");
        }
        catch (Exception ex)
        {
            logger.ExLogErrorException(ex, "Handled exception via ExLogger");
            logger.ExLogCriticalException(ex, "Critical exception via ExLogger");

            ExLogger.Log(logger, LogLevel.Error, ex, "Structured exception at {Now}", DateTime.UtcNow);
        }

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
}