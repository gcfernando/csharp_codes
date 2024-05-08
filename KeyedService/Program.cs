using KeyedService.Logic.Contract;
using KeyedService.Logic.Manager;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddKeyedScoped<INotification, EmailNotification>("email");
        services.AddKeyedScoped<INotification, PushNotification>("push");
        services.AddKeyedScoped<INotification, SmsNotification>("sms");
    })
    .Build();

await host.RunAsync();