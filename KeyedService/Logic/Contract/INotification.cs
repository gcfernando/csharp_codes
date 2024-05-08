namespace KeyedService.Logic.Contract;
public interface INotification
{
    Task NotifyAsync(string message);
}
