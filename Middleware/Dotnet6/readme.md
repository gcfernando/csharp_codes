# Azure Functions Middleware vs No Middleware — .NET 6 C# Comparison

A side-by-side comparison of two Azure Functions v4 projects on .NET 6: one using **isolated worker middleware** (`WithMiddleware`, built on `IFunctionsWorkerMiddleware`) and one without middleware (`WithoutMiddleware`, an in-process function). Both expose a `SendMessage` HTTP POST endpoint that greets the caller by name, but the middleware version transforms the name to uppercase before the function handler runs.

## Projects

| Project | Port | Description |
|---|---|---|
| `WithMiddleware` | 7052 | Registers `UppercaseNameMiddleware`; name is uppercased in middleware before reaching the handler |
| `WithoutMiddleware` | 7162 | In-process model (`Microsoft.NET.Sdk.Functions`), no middleware; the raw `name` field from the JSON body is used as-is |

Middleware is a feature of the isolated worker model, which is why the baseline project uses the in-process model.

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

### Minimum `local.settings.json` (WithMiddleware)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

For the in-process `WithoutMiddleware` project, set `"FUNCTIONS_WORKER_RUNTIME": "dotnet"` instead of `"dotnet-isolated"`.

## NuGet Packages (WithMiddleware)

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 1.19.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.0.13 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 1.14.0 |
| `Newtonsoft.Json` | 13.0.1 |

`WithoutMiddleware` references only `Microsoft.NET.Sdk.Functions` 4.2.0.

## Running Locally

From the `Middleware` folder:

```bash
# With middleware
cd Dotnet6/WithMiddleware
func start --port 7052

# Without middleware (separate terminal)
cd Dotnet6/WithoutMiddleware
func start --port 7162
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

```bash
# Without middleware — name is used as-is
curl -X POST http://localhost:7162/api/SendMessage \
  -H "Content-Type: application/json" \
  -d '{"name": "alice"}'
```

Response: `Hello, alice. This HTTP triggered function executed successfully.` (with no `name`, it asks you to pass one).

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

## Related Examples

- [.NET 8 version with exception-handling middleware](../Dotnet8/readme.md)
- [Azure Functions middleware overview](../README.md)
- [Back to all C# and .NET code examples](../../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
