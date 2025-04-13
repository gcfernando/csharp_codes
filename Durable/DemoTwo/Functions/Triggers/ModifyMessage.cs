using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DemoTwo.Functions.Triggers;
public static class ModifyMessage
{
    [Function("func-say-hello")]
    public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("func-say-hello");

        if (string.IsNullOrWhiteSpace(name))
        {
            logger.LogInformation("{Message}", "Processing name is empty or null.");
            return string.Empty;
        }

        logger.LogInformation("Saying hello to {Name}.", name);

        return $"Hello {name}!";
    }
}