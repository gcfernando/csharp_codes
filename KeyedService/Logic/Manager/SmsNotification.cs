using KeyedService.Logic.Contract;
using Microsoft.Extensions.Logging;

namespace KeyedService.Logic.Manager;
public class SmsNotification(ILogger<SmsNotification> logger) : INotification
{
    // Injected dependency for logging (using ILogger)
    private readonly ILogger<SmsNotification> _logger = logger;

    public async Task NotifyAsync(string message)
    {
        // Log information about sending an SMS notification with the message content
        _logger.LogInformation("Notify by SMS: {smsMessage}", message);

        // Simulate SMS sending by awaiting a completed task (replace with actual logic)
        await Task.CompletedTask;
    }
}
