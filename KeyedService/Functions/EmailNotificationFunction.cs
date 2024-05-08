using System.Net;
using System.Text;
using KeyedService.Logic.Contract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyedService.Functions;

public class EmailNotificationFunction(
    [FromKeyedServices("email")] INotification notification,
    ILogger<EmailNotificationFunction> logger)
{
    // Injected dependency for the notification service (keyed to "email")
    private readonly INotification _notification = notification;

    // Injected dependency for logging (using ILogger)
    private readonly ILogger<EmailNotificationFunction> _logger = logger;

    // Function attribute specifying the function name and trigger (HTTP GET with Function level authorization)
    [Function("emailnotificationfunction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        // Log information about the function processing a request
        _logger.LogInformation("Email notification function processed a request.");

        // Call the notification service to send a notification with a specific subject
        await _notification.NotifyAsync("Subject: Weekly Update: Important News, Upcoming Deadlines, and Action Items");

        // Create a success response with appropriate headers
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        // Write a message to the response body indicating success
        await response.WriteStringAsync("notification sent [EMAIL]", Encoding.UTF8);

        // Return the constructed HTTP response
        return response;
    }
}