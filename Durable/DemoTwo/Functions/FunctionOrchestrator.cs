using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DemoTwo.Functions;

public static class FunctionOrchestrator
{
    [Function("func-message-orchestrator")]
    public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger("func-message-orchestrator");

        var payLoad = context.GetInput<string>();
        var name = JObject.Parse(payLoad)["name"]?.ToString();

        logger.LogInformation("{Message}", $"Received name: {name}");

        var validatedInputValue =  await context.CallActivityAsync<string>("func-validate-input", name);
        var processedInputValue = await context.CallActivityAsync<string>("func-process-input", validatedInputValue);

        return await context.CallActivityAsync<string>("func-say-hello", processedInputValue);
    }
}
