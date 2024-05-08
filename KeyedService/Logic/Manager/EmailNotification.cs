using KeyedService.Logic.Contract;
using Microsoft.Extensions.Logging;

namespace KeyedService.Logic.Manager;
public class EmailNotification(ILogger<EmailNotification> logger) : INotification
{
    private readonly ILogger<EmailNotification> _logger = logger;

    public async Task NotifyAsync(string message)
    {
        _logger.LogInformation("Notify by email : {mailMessage}", message);

        await Task.CompletedTask;
    }
}