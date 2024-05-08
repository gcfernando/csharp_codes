using KeyedService.Logic.Contract;
using Microsoft.Extensions.Logging;

namespace KeyedService.Logic.Manager;
public class PushNotification(ILogger<PushNotification> logger) : INotification
{
    // Injected dependency for logging (using ILogger)
    private readonly ILogger<PushNotification> _logger = logger;

    public async Task NotifyAsync(string message)
    {
        // Log information about sending a push notification with the message content
        _logger.LogInformation("Notify by push: {pushMessage}", message);

        // Simulate push notification sending by awaiting a completed task (replace with actual logic)
        await Task.CompletedTask;
    }
}