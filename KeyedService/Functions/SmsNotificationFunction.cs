using System.Net;
using System.Text;
using KeyedService.Logic.Contract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyedService.Functions;

public class SmsNotificationFunction(
    [FromKeyedServices("sms")] INotification notification,
    ILogger<SmsNotificationFunction> logger)
{
    // Injected dependency for the notification service (keyed to "sms")
    private readonly INotification _notification = notification;

    // Injected dependency for logging (using ILogger)
    private readonly ILogger<SmsNotificationFunction> _logger = logger;

    // Function attribute specifying the function name and trigger (HTTP GET with Function level authorization)
    [Function("smsnotificationfunction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        // Log information about the function processing a request
        _logger.LogInformation("SMS notification function processed a request.");

        // Call the notification service to send an SMS notification with a concise message
        await _notification.NotifyAsync("Hi! Quick update: Check your email for important news and tasks this week.");

        // Create a success response with appropriate headers
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        // Write a message to the response body indicating success
        await response.WriteStringAsync("notification sent [SMS]", Encoding.UTF8);

        // Return the constructed HTTP response
        return response;
    }
}