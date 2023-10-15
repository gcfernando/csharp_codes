using Microsoft.Extensions.Hosting;
using WithMiddleware.Middlewares;

// Creates a new instance of HostBuilder for hosting functions.
var host = new HostBuilder()
    // Configures the default worker options for functions and adds the custom UppercaseNameMiddleware to the middleware pipeline.
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseMiddleware<UppercaseNameMiddleware>())
    // Configures any additional services and dependency injections required for the functions.
    .ConfigureServices(_ =>
    {
        // Register services and Dependency injections
    })
    // Builds the host configuration.
    .Build();

// Starts the host and waits for it to finish processing functions.
await host.RunAsync();