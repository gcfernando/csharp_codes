using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Middleware.Functions;

public class SendMessageFunction(ILogger<SendMessageFunction> logger)
{
    private readonly ILogger<SendMessageFunction> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    [Function("SendMessageFunction")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        // Retrieves the updated JObject from the function context's Items dictionary.
        dynamic dataObject = req.FunctionContext.Items["updated_body"];
        // Retrieves the value of the "name" key from the JObject, using the null conditional operator to handle null values.
        var name = dataObject?.name;

        if (name.ToString().Equals("name not found"))
        {
            throw new InvalidProcessException("name not found");
        }

        // Generates the response message based on whether the "name" value is empty or not.
        var responseMessage = $"Hello {name}.";

        // Creates an HTTP response with a 200 (OK) status code.
        var response = req.CreateResponse(HttpStatusCode.OK);
        // Adds a Content-Type header to the HTTP response indicating the response body's content type as plain text with UTF-8 encoding.
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        // Writes the response message to the response body using UTF-8 encoding.
        await response.WriteStringAsync(responseMessage, Encoding.UTF8);

        // Returns the HTTP response to the caller as the result of the HTTP trigger function.
        return response;
    }
}