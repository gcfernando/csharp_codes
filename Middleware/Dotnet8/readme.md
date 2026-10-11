# Azure Functions Exception Handling Middleware in C# — .NET 8 Isolated Worker

An Azure Functions v4 project on .NET 8 that demonstrates two **isolated worker middleware** components working together: `UppercaseNameMiddleware` transforms the request body before the handler, and `ExceptionHandlerMiddleware` catches exceptions thrown by the function and returns a structured `ProblemDetails` response with HTTP 400 — a reusable pattern for **global exception handling in Azure Functions**.

## Middleware Pipeline

Middleware runs in the order it is registered in `Program.cs` (`UseMiddleware<UppercaseNameMiddleware>()` first, then `UseMiddleware<ExceptionHandlerMiddleware>()`):

```
HTTP Request
     │
     ▼
UppercaseNameMiddleware      ← reads body, uppercases "name", stores in context.Items
     │
     ▼
ExceptionHandlerMiddleware   ← wraps the function; catches its exceptions, returns 400 ProblemDetails
     │
     ▼
SendMessageFunction          ← reads updated_body; throws InvalidProcessException if name not found
```

Because `ExceptionHandlerMiddleware` is registered second, it does not catch exceptions raised inside `UppercaseNameMiddleware` (for example, a request body that is not valid JSON). Register it first if you want it to wrap the whole pipeline.

## Components

### UppercaseNameMiddleware

- Copies the request body into a `MemoryStream` and reads the JSON from that copy
- Parses JSON and uppercases the `name` property (or sets it to `"name not found"` if absent)
- Stores the result in `context.Items["updated_body"]`
- Calls `next.Invoke(context)` to continue the pipeline

### ExceptionHandlerMiddleware

- Wraps `await next.Invoke(context)` in a try/catch
- On exception, logs the error with `FunctionUtility.LogCustomException` (title, message, inner exception, stack trace)
- Calls `FunctionUtility.CreateErrorResponse` to write a `ProblemDetails` JSON response with HTTP 400
- Handles both `$return` and named output bindings

### SendMessageFunction

- Reads `context.Items["updated_body"]` for the transformed name
- Throws `InvalidProcessException("name not found")` if the name was not found in the original body
- Returns `Hello {name}.` on success

### InvalidProcessException

Custom exception class with an optional `ProcessName` property, used to signal that the name field was missing or invalid.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- A `local.settings.json` in the project folder (not committed to source control)

### Minimum `local.settings.json`

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

## NuGet Packages

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 1.21.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.1.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 1.2.1 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 1.17.2 |
| `Microsoft.ApplicationInsights.WorkerService` | 2.22.0 |
| `Microsoft.Azure.Functions.Worker.ApplicationInsights` | 1.2.0 |
| `Newtonsoft.Json` | 13.0.3 |

## Running Locally

```bash
cd Dotnet8/Middleware
func start --port 7256
```

## Usage

```bash
# Valid request — name is uppercased
curl -X POST http://localhost:7256/api/SendMessageFunction \
  -H "Content-Type: application/json" \
  -d '{"name": "alice"}'
```

Response: `Hello ALICE.`

```bash
# Missing name — exception handler returns 400 ProblemDetails
curl -X POST http://localhost:7256/api/SendMessageFunction \
  -H "Content-Type: application/json" \
  -d '{}'
```

Response:

```json
{
  "status": 400,
  "type": "InvalidProcessException",
  "title": "Cannot process request",
  "detail": "name not found"
}
```

## Project Structure

```
Dotnet8/
└── Middleware/
    ├── Functions/
    │   └── SendMessageFunction.cs          # Reads updated_body; throws on missing name
    ├── Middlewares/
    │   ├── UppercaseNameMiddleware.cs       # Transforms name; stores in context.Items
    │   └── ExceptionHandlerMiddleware.cs   # Catches exceptions; returns ProblemDetails
    ├── Utility/
    │   └── FunctionUtility.cs              # LogCustomException + CreateErrorResponse helpers
    ├── InvalidProcessException.cs          # Custom exception with ProcessName property
    ├── Program.cs                          # Middleware registration order
    └── Middleware.csproj
```

## Related Examples

- [.NET 6 comparison: with and without middleware](../Dotnet6/readme.md)
- [Azure Functions middleware overview](../README.md)
- [Back to all C# and .NET code examples](../../README.md)
