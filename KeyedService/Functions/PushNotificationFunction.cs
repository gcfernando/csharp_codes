using System.Net;
using System.Text;
using KeyedService.Logic.Contract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyedService.Functions;

public class PushNotificationFunction(
    [FromKeyedServices("push")] INotification notification,
    ILogger<PushNotificationFunction> logger)
{
    // Injected dependency for the notification service (keyed to "push")
    private readonly INotification _notification = notification;

    // Injected dependency for logging (using ILogger)
    private readonly ILogger<PushNotificationFunction> _logger = logger;

    // Function attribute specifying the function name and trigger (HTTP GET with Function level authorization)
    [Function("pushnotificationfunction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        // Log information about the function processing a request
        _logger.LogInformation("Push notification function processed a request.");

        // Call the notification service to send a push notification with a message
        await _notification.NotifyAsync("New updates available! Check now for the latest news and tasks.");

        // Create a success response with appropriate headers
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        // Write a message to the response body indicating success
        await response.WriteStringAsync("notification sent [PUSH]", Encoding.UTF8);

        // Return the constructed HTTP response
        return response;
    }
}