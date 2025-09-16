using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

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

        // Without Arguments
        ExLogger.LogTrace(_logger, "This is a trace message from ExLogger.");
        ExLogger.LogDebug(_logger, "This is a debug message from ExLogger.");
        ExLogger.LogInformation(_logger, "This is an informational message from ExLogger.");
        ExLogger.LogWarning(_logger, "This is a warning message from ExLogger.");
        ExLogger.LogError(_logger, "This is an error message from ExLogger.");

        // With Arguments naming
        ExLogger.LogInformation(_logger, "This is an informational message executed at {dateTime}.", DateTime.UtcNow);

        // With Arguments numbering
        ExLogger.LogWarning(_logger, "This is an warning message executed at {0}.", DateTime.UtcNow);

        // Log Without Arguments
        ExLogger.Log(_logger, LogLevel.Warning, "This is an log method without arguments.");

        // Log With Arguments
        ExLogger.Log(_logger, LogLevel.Error, "This is an log method with arguments executed at {dateTime}.", DateTime.UtcNow);

        try
        {
            const int counter = 5;
            _ = counter / int.Parse("0");
        }
        catch (Exception ex)
        {
            ExLogger.LogException(_logger, ex);
        }

        return new OkObjectResult("Logger Excuted Successfully.");
    }
}
