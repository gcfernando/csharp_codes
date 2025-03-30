using Mediat.Business.Queries.UserQuery;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mediat.AzureFunction;

public class GetUserFunction(IMediator mediator, ILogger<GetUserFunction> logger)
{
    private readonly IMediator _mediator = mediator;
    private readonly ILogger<GetUserFunction> _logger = logger;

    [Function("get-user-function")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{id}")] HttpRequest req, int id)
    {
        if (req is null)
        {
            return new BadRequestObjectResult("Invalid request");
        }

        _logger.LogInformation("Received request to fetch user with ID: {UserId}", id);

        var query = new GetUserQuery { UserId = id };
        var user = await _mediator.Send(query);

        if (user is null)
        {
            _logger.LogWarning("User with ID {UserId} not found", id);
            return new NotFoundResult();
        }

        _logger.LogInformation("User with ID {UserId} retrieved successfully", id);

        return new OkObjectResult(user);
    }
}