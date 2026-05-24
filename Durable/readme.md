# Durable Functions

Two Azure Durable Functions v4 demos that demonstrate stateful, chained orchestration pipelines using the isolated worker model on .NET 8.

## Projects

| Project | Port | Pipeline |
|---|---|---|
| [DemoOne](./DemoOne) | 7239 | HTTP → Orchestrator → 1 activity (greeting) |
| [DemoTwo](./DemoTwo) | 7176 | HTTP → Orchestrator → 3 activities (validate → process → greet) |

## What Are Durable Functions?

Azure Durable Functions is an extension of Azure Functions that lets you write stateful workflows in a serverless environment. Orchestrator functions define the workflow logic and call activity functions, while the Durable Task Framework manages state, retries, and checkpointing automatically.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator) or an Azure Storage account

## Shared NuGet Packages

Both projects use the same package versions:

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.0.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | 1.2.3 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.3.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 2.0.1 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.2 |

## Minimum `local.settings.json`

Each project requires its own `local.settings.json` (not committed to source control):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

## Quick Start

```bash
# Run DemoOne
cd DemoOne
func start --port 7239

# Run DemoTwo (in a separate terminal)
cd DemoTwo
func start --port 7176
```

See each project's own README for detailed usage and endpoint examples.
