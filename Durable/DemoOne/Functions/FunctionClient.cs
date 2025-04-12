using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DemoOne.Functions;

public class FunctionClient
{
    private readonly ILogger<FunctionClient> _logger;

    public FunctionClient(ILogger<FunctionClient> logger) =>
        _logger = logger;

    [Function("func-message-validator")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
                                              [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("{Message}", "C# HTTP trigger function 'func-message-validator' received a request.");

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        if (string.IsNullOrEmpty(requestBody))
        {
            _logger.LogWarning("{Message}", "Request body is empty.");

            return new BadRequestObjectResult("Request body cannot be empty.");
        }

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "func-message-orchestrator", requestBody);

        _logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        var response = await client.WaitForInstanceCompletionAsync(instanceId, true);

        return new OkObjectResult($"{response.SerializedOutput}");
    }
}
