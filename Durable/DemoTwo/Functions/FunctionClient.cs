using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DemoTwo.Functions;

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

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "func-message-orchestrator", requestBody);

        _logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        var response = await client.WaitForInstanceCompletionAsync(instanceId, true);

        if (response.SerializedOutput.Contains(""))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Request body cannot be processed.");
            return new BadRequestResult();
        }

        return new OkObjectResult($"{response.SerializedOutput}");
    }
}