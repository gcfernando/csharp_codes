# Durable Functions — DemoOne

A minimal Azure Durable Functions v4 demo that runs a single-activity orchestration pipeline. An HTTP trigger receives a JSON body, starts an orchestrator, and the orchestrator calls one activity that returns a personalised greeting.

## Architecture

```
POST /api/func-message-validator
          │
          ▼
 func-message-validator        (HTTP client trigger)
          │  schedules orchestration
          ▼
 func-message-orchestrator     (orchestrator)
          │  calls activity
          ▼
 func-say-hello                (activity — returns "Hello {name}!")
```

## Functions

| Function | Trigger | Description |
|---|---|---|
| `func-message-validator` | HTTP POST | Reads request body, starts orchestration, waits for completion, returns result |
| `func-message-orchestrator` | Orchestration | Extracts `name` from JSON payload, calls `func-say-hello` |
| `func-say-hello` | Activity | Returns `"Hello {name}!"` |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- A `local.settings.json` file in the project root (not committed to source control)

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

> Requires [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) or a real Azure Storage connection string for durable task state.

## NuGet Packages

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.0.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | 1.2.3 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.3.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 2.0.1 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.2 |

## Running Locally

```bash
cd DemoOne
func start --port 7239
```

## Usage

```bash
curl -X POST http://localhost:7239/api/func-message-validator \
  -H "Content-Type: application/json" \
  -d '{"name": "Alice"}'
```

**Success response:**

```
"Hello Alice!"
```

**Empty body returns 400:**

```
Request body cannot be empty.
```

## Project Structure

```
DemoOne/
├── Functions/
│   ├── FunctionClient.cs          # HTTP trigger — starts orchestration
│   ├── FunctionOrchestrator.cs    # Orchestrator — extracts name, calls activity
│   └── Triggers/
│       └── ModifyMessage.cs       # Activity (func-say-hello)
├── Properties/
│   └── launchSettings.json        # Default port: 7239
├── Program.cs                     # Host builder entry point
├── host.json
└── DemoOne.csproj
```
