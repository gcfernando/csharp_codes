using System.Net;
using System.Text;
using KeyedService.Logic.Contract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyedService.Functions;

public class PushNotificationFunction([FromKeyedServices("push")] INotification notification, ILogger<PushNotificationFunction> logger)
{
    private readonly INotification _notification = notification;
    private readonly ILogger<PushNotificationFunction> _logger = logger;

    [Function("pushnotificationfunction")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Push notification function processed a request.");

        await _notification.NotifyAsync("New updates available! Check now for the latest news and tasks.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync("notification sent [PUSH]", Encoding.UTF8);

        return response;
    }
}
