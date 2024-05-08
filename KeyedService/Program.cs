using KeyedService.Logic.Contract;
using KeyedService.Logic.Manager;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Create a new HostBuilder instance to configure the Azure Functions application.
var host = new HostBuilder()
    // Configure the host to build a WebApplication suitable for Azure Functions.
    .ConfigureFunctionsWebApplication()
    // Configure the services that will be available to be injected into functions.
    .ConfigureServices(services =>
    {
        // Add Application Insights Telemetry service for function instrumentation.
        services.AddApplicationInsightsTelemetryWorkerService();

        // Configure Application Insights for the Functions application.
        services.ConfigureFunctionsApplicationInsights();

        // Register different notification implementations with keyed scoped lifetime.
        // This means a single instance will be created per notification type ("email", "push", or "sms")
        // and shared within the same scope (likely a function execution).
        services.AddKeyedScoped<INotification, EmailNotification>("email");
        services.AddKeyedScoped<INotification, PushNotification>("push");
        services.AddKeyedScoped<INotification, SmsNotification>("sms");
    })
    // Build the host instance with the configured services.
    .Build();

// Run the Azure Functions application asynchronously.
await host.RunAsync();