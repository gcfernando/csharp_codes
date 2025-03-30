using Mediat.Business.Commands.UserCommand;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Mediat.AzureFunction;

public class CreateUserFunction(IMediator mediator, ILogger<CreateUserFunction> logger)
{
    private readonly IMediator _mediator = mediator;
    private readonly ILogger<CreateUserFunction> _logger = logger;

    [Function("create-user-function")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = "user")] HttpRequest req)
    {
        _logger.LogInformation("Received request to create a new user.");

        if (req is null)
        {
            _logger.LogWarning("Received an invalid request.");
            return new BadRequestObjectResult("Invalid request");
        }

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        _logger.LogInformation("Request body read successfully.");

        var command = JsonConvert.DeserializeObject<CreateUserCommand>(requestBody);

        if (command is null)
        {
            _logger.LogWarning("Deserialization failed. Invalid request body.");
            return new BadRequestObjectResult("Invalid data format");
        }
        _logger.LogInformation("Request body deserialized into CreateUserCommand.");

        var user = await _mediator.Send(command);
        _logger.LogInformation("User created successfully with ID: {UserId}", user?.Id);

        return new OkObjectResult(user);
    }
}