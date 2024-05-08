using KeyedService.Logic.Contract;
using Microsoft.Extensions.Logging;

namespace KeyedService.Logic.Manager;
public class PushNotification(ILogger<PushNotification> logger) : INotification
{
    private readonly ILogger<PushNotification> _logger = logger;

    public async Task NotifyAsync(string message)
    {
        _logger.LogInformation("Notify by push : {pushMessage}", message);

        await Task.CompletedTask;
    }
}