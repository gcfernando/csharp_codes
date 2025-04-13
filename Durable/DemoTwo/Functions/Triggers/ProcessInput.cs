using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DemoTwo.Functions.Triggers;
public static class ProcessInput
{
    [Function("func-process-input")]
    public static string Run([ActivityTrigger] string input, FunctionContext context)
    {
        var logger = context.GetLogger("func-process-input");

        if (string.IsNullOrWhiteSpace(input))
        {
            logger.LogInformation("{Message}", "Processing input is empty or null.");
            return string.Empty;
        }

        logger.LogInformation("Processing input: {Input}", input);

        return string.IsNullOrWhiteSpace(input) ? string.Empty : input;
    }
}