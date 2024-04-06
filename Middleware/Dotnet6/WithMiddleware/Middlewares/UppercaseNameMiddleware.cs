using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WithMiddleware.Middlewares;
public class UppercaseNameMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<UppercaseNameMiddleware> _logger;

    public UppercaseNameMiddleware(ILogger<UppercaseNameMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Defines a constant string key for the JSON key to be modified.
        const string key = "name";
        // Retrieves the HTTP request data from the function context.
        var requestData = await context.GetHttpRequestDataAsync();
        // Reads the HTTP request body as a string.
        var body = await new StreamReader(requestData.Body).ReadToEndAsync();

        _logger.LogInformation("Reading HTTP data {body}", body);

        var dataObject = JObject.Parse(body);

        if (dataObject.ContainsKey(key))
        {
            // Converts the value of the "name" key to uppercase.
            dataObject[key] = dataObject?[key]
                .ToString()
                .ToUpper();

            _logger.LogInformation("modify name key");
        }
        else
        {
            dataObject[key] = "name not found";
        }

        // Adds the updated JObject to the function context's Items dictionary.
        context.Items.Add("updated_body", dataObject);
        // Resets the position of the HTTP request body stream to the beginning.
        requestData.Body.Position = 0;

        _logger.LogInformation("Update context");

        // Calls the next function in the pipeline with the updated function context.
        await next(context);
    }
}