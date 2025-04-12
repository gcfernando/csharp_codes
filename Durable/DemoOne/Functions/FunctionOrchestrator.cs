using DemoOne.Functions.Triggers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DemoOne.Functions;

public static class FunctionOrchestrator
{
    [Function("func-message-orchestrator")]
    public static Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger("func-message-orchestrator");

        var payLoad = context.GetInput<string>();
        var name = JObject.Parse(payLoad)["name"]?.ToString();

        logger.LogInformation("{Message}", $"Received name: {name}");

        return context.CallActivityAsync<string>("say-hello", name);
    }
}
