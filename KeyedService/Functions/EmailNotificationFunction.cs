using System.Net;
using System.Text;
using KeyedService.Logic.Contract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyedService.Functions;

public class EmailNotificationFunction([FromKeyedServices("email")] INotification notification, ILogger<EmailNotificationFunction> logger)
{
    private readonly INotification _notification = notification;
    private readonly ILogger<EmailNotificationFunction> _logger = logger;

    [Function("emailnotificationfunction")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Email notification function processed a request.");

        await _notification.NotifyAsync("Subject: Weekly Update: Important News, Upcoming Deadlines, and Action Items");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync("notification sent [EMAIL]", Encoding.UTF8);

        return response;
    }
}
