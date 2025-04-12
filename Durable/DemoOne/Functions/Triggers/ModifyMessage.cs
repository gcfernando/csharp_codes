using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DemoOne.Functions.Triggers;
public static class ModifyMessage
{
    [Function("say-hello")]
    public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("say-hello");
        logger.LogInformation("Saying hello to {Name}.", name);

        return $"Hello {name}!";
    }
}
