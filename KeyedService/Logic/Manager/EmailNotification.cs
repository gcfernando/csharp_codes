using KeyedService.Logic.Contract;
using Microsoft.Extensions.Logging;

namespace KeyedService.Logic.Manager;
public class EmailNotification(ILogger<EmailNotification> logger) : INotification
{
    // Injected dependency for logging (using ILogger)
    private readonly ILogger<EmailNotification> _logger = logger;

    public async Task NotifyAsync(string message)
    {
        // Log information about sending an email notification with the message content
        _logger.LogInformation("Notify by email: {mailMessage}", message);

        // Simulate email sending by awaiting a completed task (replace with actual email sending logic)
        await Task.CompletedTask;
    }
}