using System.Net;
using System.Text;
using KeyedService.Logic.Contract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyedService.Functions;

public class SmsNotificationFunction([FromKeyedServices("sms")] INotification notification, ILogger<SmsNotificationFunction> logger)
{
    private readonly INotification _notification = notification;
    private readonly ILogger<SmsNotificationFunction> _logger = logger;

    [Function("smsnotificationfunction")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("SMS notification function processed a request.");

        await _notification.NotifyAsync("Hi! Quick update: Check your email for important news and tasks this week.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync("notification sent [SMS]", Encoding.UTF8);

        return response;
    }
}
