# Azure Functions Middleware Examples — .NET 6 and .NET 8 Isolated Worker (C#)

Runnable C# examples of **Azure Functions middleware** with `IFunctionsWorkerMiddleware`. They show how to intercept an HTTP-triggered function in the **.NET isolated worker model**, transform the JSON request body before the function runs, share data through `FunctionContext.Items`, and turn unhandled exceptions into a structured `ProblemDetails` error response.

There are two versions: a **.NET 6** comparison of the same function written with and without middleware, and a **.NET 8** project that combines request-transforming middleware with exception-handling middleware.

## Structure

| Folder | Target | Hosting model | Description |
|---|---|---|---|
| [Dotnet6/WithoutMiddleware](./Dotnet6/WithoutMiddleware) | .NET 6, Functions v4 | In-process (`Microsoft.NET.Sdk.Functions`) | Baseline: the function reads the `name` from the request itself, with no middleware |
| [Dotnet6/WithMiddleware](./Dotnet6/WithMiddleware) | .NET 6, Functions v4 | Isolated worker | `UppercaseNameMiddleware` parses and uppercases the `name` before the function runs |
| [Dotnet8/Middleware](./Dotnet8/Middleware) | .NET 8, Functions v4 | Isolated worker (ASP.NET Core integration) | `UppercaseNameMiddleware` plus `ExceptionHandlerMiddleware` and a custom error-response utility |

Middleware is only available in the isolated worker model, which is why the .NET 6 baseline uses the in-process model for comparison.

## What Is Azure Functions Middleware?

In the isolated worker model, middleware is a class that implements `IFunctionsWorkerMiddleware` and is registered in `Program.cs`. It runs for every function invocation and can execute code before and after the function handler. Middleware can read and modify the `FunctionContext`, read the HTTP request body, add values to `context.Items`, and catch exceptions thrown further down the pipeline.

The .NET 8 project registers its middleware like this ([`Program.cs`](./Dotnet8/Middleware/Program.cs)):

```csharp
var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
        builder.AddEnvironmentVariables())
    .ConfigureFunctionsWebApplication(worker =>
    {
        // Middleware Sequence
        worker.UseMiddleware<UppercaseNameMiddleware>();
        worker.UseMiddleware<ExceptionHandlerMiddleware>();
    })
    // ...
    .Build();
```

## How the .NET 8 Example Works

Middleware runs in the order it is registered:

1. **`UppercaseNameMiddleware`** copies the request body to a `MemoryStream`, parses it as JSON, uppercases the `name` property (or sets it to `"name not found"` when it is missing), stores the result in `context.Items["updated_body"]`, and calls the next step.
2. **`ExceptionHandlerMiddleware`** wraps the function call in `try`/`catch`. If the function throws, it logs the error and writes an HTTP 400 response containing a `ProblemDetails` object.
3. **`SendMessageFunction`** (HTTP `POST`) reads `updated_body` from `FunctionContext.Items`. It returns `Hello {NAME}.` as plain text, or throws `InvalidProcessException` when no name was supplied — which the exception middleware converts into the 400 response.

Because `ExceptionHandlerMiddleware` is registered after `UppercaseNameMiddleware`, it handles exceptions from the function, not exceptions raised inside `UppercaseNameMiddleware` itself (for example, a request body that is not valid JSON).

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) and/or [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) v4

## Quick Start

From the repository root:

```bash
# .NET 6 demo (isolated worker with middleware)
cd Middleware/Dotnet6/WithMiddleware
func start --port 7052

# .NET 8 demo
cd Middleware/Dotnet8/Middleware
func start --port 7256
```

The ports match each project's `launchSettings.json`, so running from Visual Studio uses the same addresses. Send a JSON body with a `name` property in a `POST` request to the function URL that `func start` prints.

See each sub-folder's readme ([.NET 6](./Dotnet6/readme.md), [.NET 8](./Dotnet8/readme.md)) for detailed usage and sample requests.

## Related Examples

- [Keyed dependency injection in Azure Functions](../KeyedService/)
- [MediatR and CQRS in Azure Functions](../Mediat/)
- [Durable Functions orchestration](../Durable/)
- [Back to the C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
