# Durable Functions — DemoTwo

An Azure Durable Functions v4 demo with a three-activity orchestration pipeline. An HTTP trigger starts an orchestrator that chains three activity functions: input validation, processing, and greeting generation.

## Architecture

```
POST /api/func-message-validator
          │
          ▼
 func-message-validator        (HTTP client trigger)
          │  schedules orchestration
          ▼
 func-message-orchestrator     (orchestrator)
          │
          ├─► func-validate-input   (validates: letters and spaces only)
          │
          ├─► func-process-input    (passes value through; returns empty if blank)
          │
          └─► func-say-hello        (returns "Hello {name}!" or empty string)
```

## Functions

| Function | Trigger | Description |
|---|---|---|
| `func-message-validator` | HTTP POST | Starts orchestration, waits for result, returns 200 or 400 |
| `func-message-orchestrator` | Orchestration | Chains the three activity functions |
| `func-validate-input` | Activity | Accepts only letters and spaces (`^[A-Za-z\s]+$`); returns empty string if invalid |
| `func-process-input` | Activity | Passes the value through; returns empty string if input is blank |
| `func-say-hello` | Activity | Returns `"Hello {name}!"` or empty string if name is blank |

## Validation Rule

The `func-validate-input` activity enforces the pattern `^[A-Za-z\s]+$`. Any name containing digits or special characters will be rejected and an empty string will propagate through the pipeline, resulting in a 400 response.

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
cd DemoTwo
func start --port 7176
```

## Usage

**Valid name:**

```bash
curl -X POST http://localhost:7176/api/func-message-validator \
  -H "Content-Type: application/json" \
  -d '{"name": "Alice"}'
```

Response: `"Hello Alice!"`

**Invalid name (digits or special characters):**

```bash
curl -X POST http://localhost:7176/api/func-message-validator \
  -H "Content-Type: application/json" \
  -d '{"name": "Alice123"}'
```

Response: `400 Bad Request`

## Project Structure

```
DemoTwo/
├── Functions/
│   ├── FunctionClient.cs          # HTTP trigger — starts orchestration
│   ├── FunctionOrchestrator.cs    # Orchestrator — chains three activities
│   └── Triggers/
│       ├── ValidateInput.cs       # Activity: regex validation
│       ├── ProcessInput.cs        # Activity: pass-through / blank check
│       └── ModifyMessage.cs       # Activity: greeting generation
├── Properties/
│   └── launchSettings.json        # Default port: 7176
├── Program.cs                     # Host builder entry point
├── host.json
└── DemoTwo.csproj
```
