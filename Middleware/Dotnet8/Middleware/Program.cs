using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Middleware.Middlewares;

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
        builder.AddEnvironmentVariables())
    .ConfigureFunctionsWebApplication(worker =>
        worker.UseMiddleware<UppercaseNameMiddleware>())
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

await host.RunAsync();