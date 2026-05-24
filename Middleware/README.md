# Azure Functions Middleware

Two demonstrations of **Azure Functions middleware** using the isolated worker model — one targeting .NET 6 and one targeting .NET 8. Each example shows how to intercept and transform HTTP requests before they reach the function handler.

## Structure

| Folder | Target | Description |
|---|---|---|
| [Dotnet6](./Dotnet6) | .NET 6 | Comparison of a function with and without middleware |
| [Dotnet8](./Dotnet8) | .NET 8 | Middleware with exception handling and a custom utility |

## What Is Functions Middleware?

Azure Functions isolated worker middleware implements `IFunctionsWorkerMiddleware`. Middleware is registered in `Program.cs` and runs for every function invocation, before and after the handler. It can read and modify the `FunctionContext`, inspect the request body, add values to `context.Items`, and intercept exceptions.

## Quick Start

```bash
# .NET 6 demo
cd Middleware/Dotnet6/WithMiddleware
func start --port 7052

# .NET 8 demo
cd Middleware/Dotnet8/Middleware
func start --port 7256
```

See each sub-folder's README for detailed usage.
