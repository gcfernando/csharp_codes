using System.Reflection;
using Mediat.Data.Contract;
using Mediat.Data.Repository;
using Mediat.Infrastructure;
using MediatR;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((_, builder) => ConfigureAppSettings(builder))
    .ConfigureServices(ConfigureServices)
    .Build();

await host.RunAsync()
    .ConfigureAwait(false);

static void ConfigureAppSettings(IConfigurationBuilder builder)
{
    builder.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();
}

static void ConfigureServices(IServiceCollection services)
{
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
        Assembly.GetExecutingAssembly(),        // Azure Function Project (Mediat.AzureFunction)
        Assembly.Load("Mediat.Business"),       // Business Layer (Mediat.Business)
        Assembly.Load("Mediat.Infrastructure")  // Infrastructure Layer (Mediat.Infrastructure) where pipeline behaviors reside
    ));

    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceMonitoringBehavior<,>));

    services.AddSingleton<IUserRepository, UserRepository>();
}