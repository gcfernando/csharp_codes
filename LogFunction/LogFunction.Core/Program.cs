using System.Text;
using LogFunction.Logger;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

#region Logging Configuration

// Configure logging with scopes and timestamps
builder.Services.AddLogging(loggingBuilder =>
{
    _ = loggingBuilder.ClearProviders();
    _ = loggingBuilder.AddConsole(options =>
        options.FormatterName = ConsoleFormatterNames.Simple);
});

builder.Services.Configure<SimpleConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";
    options.SingleLine = false;
});

#endregion Logging Configuration

#region Application Insights

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

#endregion Application Insights

#region ExLogger - Global Exception Formatter Configuration

// Option 1: Use the default internal ExLogger exception formatter
var useDefaultFormatter = false;

// Option 2: Provide a custom global exception formatter
if (!useDefaultFormatter)
{
    ExLogger.ExceptionFormatter = (ex, title, detailed) =>
    {
        var sb = new StringBuilder(512);
        _ = sb.AppendLine("==== Custom Exception Log ====")
              .AppendLine($"Timestamp  : {DateTime.UtcNow:O}")
              .AppendLine($"Title      : {title}")
              .AppendLine($"Type       : {ex.GetType().FullName}")
              .AppendLine($"Message    : {ex.Message}")
              .AppendLine($"HResult    : {ex.HResult}")
              .AppendLine($"Source     : {ex.Source ?? "N/A"}")
              .AppendLine($"TargetSite : {ex.TargetSite?.Name ?? "N/A"}");

        if (detailed && !string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            _ = sb.AppendLine()
                  .AppendLine("Stack Trace:")
                  .AppendLine(ex.StackTrace.Trim());
        }

        if (ex.InnerException is not null)
        {
            _ = sb.AppendLine()
                  .AppendLine("---- Inner Exception ----")
                  .AppendLine($"Type    : {ex.InnerException.GetType().FullName}")
                  .AppendLine($"Message : {ex.InnerException.Message}");
        }

        _ = sb.AppendLine("===============================");
        return sb.ToString();
    };
}
else
{
    // Revert to the built-in formatter (default behavior)
    ExLogger.ExceptionFormatter = null!;
}

#endregion ExLogger - Global Exception Formatter Configuration

await builder
    .Build()
    .RunAsync();