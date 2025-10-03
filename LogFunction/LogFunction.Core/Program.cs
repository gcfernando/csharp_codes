using LogFunction.Logger;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Enable console logging with scopes in .NET 8
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddConsole(options =>
        options.FormatterName = ConsoleFormatterNames.Simple);
});

// Register BatchLogger as a singleton wrapper around ILoggerFactory
builder.Services.AddSingleton<BatchLogger>(sp =>
{
    var factory = sp.GetRequiredService<ILoggerFactory>();
    var innerLogger = factory.CreateLogger("BatchLogger");

    return new BatchLogger(
        innerLogger,
        capacity: 10_000,                   // adjust based on load
        batchSize: 200,                     // flush when 200 logs are queued
        flushInterval: TimeSpan.FromMilliseconds(250) // or time-based flush
    );
});

builder.Services.Configure<SimpleConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";
    options.SingleLine = false;
});

// Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

await builder.Build().RunAsync();