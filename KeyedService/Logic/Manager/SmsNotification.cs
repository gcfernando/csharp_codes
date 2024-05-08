using KeyedService.Logic.Contract;
using Microsoft.Extensions.Logging;

namespace KeyedService.Logic.Manager;
public class SmsNotification(ILogger<SmsNotification> logger) : INotification
{
    private readonly ILogger<SmsNotification> _logger = logger;
    public async Task NotifyAsync(string message)
    {
        _logger.LogInformation("Notify by sms : {smsMessage}", message);

        await Task.CompletedTask;
    }
}
