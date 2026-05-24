# Azure Functions Middleware — .NET 6

A side-by-side comparison of two Azure Functions v4 projects on .NET 6: one with middleware (`WithMiddleware`) and one without (`WithoutMiddleware`). Both expose a `SendMessage` HTTP POST endpoint that greets the caller by name, but the middleware version transforms the name to uppercase before the function handler runs.

## Projects

| Project | Port | Description |
|---|---|---|
| `WithMiddleware` | 7052 | Registers `UppercaseNameMiddleware`; name is uppercased in middleware before reaching the handler |
| `WithoutMiddleware` | — | No middleware; the raw `name` field from the JSON body is used as-is |

## How the Middleware Works

`UppercaseNameMiddleware` implements `IFunctionsWorkerMiddleware`:

1. Reads the HTTP request body as a string
2. Parses it as JSON (`JObject`)
3. Converts the `name` property value to uppercase (or sets it to `"name not found"` if absent)
4. Stores the updated `JObject` in `context.Items["updated_body"]`
5. Resets the request body stream position
6. Calls `next(context)` to pass control to the function handler

The `SendMessageFunction` then reads `context.Items["updated_body"]` and returns `Hello {name}.`

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- A `local.settings.json` in each project (not committed to source control)

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

## NuGet Packages (WithMiddleware)

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 1.19.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.0.13 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 1.14.0 |
| `Newtonsoft.Json` | 13.0.1 |

## Running Locally

```bash
cd Dotnet6/WithMiddleware
func start --port 7052
```

## Usage

```bash
# With middleware — name is returned in uppercase
curl -X POST http://localhost:7052/api/SendMessage \
  -H "Content-Type: application/json" \
  -d '{"name": "alice"}'
```

Response: `Hello ALICE.`

```bash
# Missing name field
curl -X POST http://localhost:7052/api/SendMessage \
  -H "Content-Type: application/json" \
  -d '{}'
```

Response: `Hello name not found.`

## Project Structure

```
Dotnet6/
├── WithMiddleware/
│   ├── Functions/
│   │   └── SendMessageFunction.cs      # Reads updated_body from context.Items
│   ├── Middlewares/
│   │   └── UppercaseNameMiddleware.cs  # Transforms name to uppercase
│   ├── Program.cs                      # Registers middleware
│   └── WithMiddleware.csproj
├── WithoutMiddleware/
│   ├── Functions/
│   │   └── SendMessageFunction.cs      # Reads name directly from request body
│   └── WithoutMiddleware.csproj
└── readme.md
```
