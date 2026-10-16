# Keyed Services in .NET 8 Dependency Injection — Azure Functions Example (C#)

An Azure Functions v4 demo on .NET 8 that demonstrates **keyed dependency injection** (`AddKeyedScoped` / `[FromKeyedServices]`) in the isolated worker model. Three HTTP endpoints each receive a different implementation of the same `INotification` interface, selected by its registered key — a way to choose between email, SMS, and push strategies without writing a factory class.

## Overview

.NET 8 introduced first-class keyed service registration in the DI container. This project shows how to register multiple implementations of the same interface under different string keys and inject the correct one into each Azure Function using the `[FromKeyedServices("key")]` attribute.

## Functions

| Function | HTTP Method | Key | Notification Type |
|---|---|---|---|
| `emailnotificationfunction` | GET | `"email"` | `EmailNotification` |
| `pushnotificationfunction` | GET | `"push"` | `PushNotification` |
| `smsnotificationfunction` | GET | `"sms"` | `SmsNotification` |

Each function calls `INotification.NotifyAsync(message)` on its injected implementation and returns a plain-text response confirming which channel was used.

## DI Registration

```csharp
services.AddKeyedScoped<INotification, EmailNotification>("email");
services.AddKeyedScoped<INotification, PushNotification>("push");
services.AddKeyedScoped<INotification, SmsNotification>("sms");
```

## Injection in Functions

```csharp
public class EmailNotificationFunction(
    [FromKeyedServices("email")] INotification notification,
    ILogger<EmailNotificationFunction> logger)
```

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
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": ""
  }
}
```

## NuGet Packages

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 1.20.1 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.1.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 1.2.0 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 1.16.4 |
| `Microsoft.ApplicationInsights.WorkerService` | 2.21.0 |
| `Microsoft.Azure.Functions.Worker.ApplicationInsights` | 1.1.0 |

## Running Locally

```bash
cd KeyedService
func start --port 7194
```

## Usage

```bash
# Email notification
curl http://localhost:7194/api/emailnotificationfunction

# Push notification
curl http://localhost:7194/api/pushnotificationfunction

# SMS notification
curl http://localhost:7194/api/smsnotificationfunction
```

Each returns a plain-text confirmation such as `notification sent [EMAIL]`.

The `EmailNotification`, `PushNotification`, and `SmsNotification` implementations only log the message (they await `Task.CompletedTask`); replace them with real email, push, or SMS providers in your own application.

## Project Structure

```text
KeyedService/
├── Functions/
│   ├── EmailNotificationFunction.cs   # Injects INotification keyed "email"
│   ├── PushNotificationFunction.cs    # Injects INotification keyed "push"
│   └── SmsNotificationFunction.cs     # Injects INotification keyed "sms"
├── Logic/
│   ├── Contract/
│   │   └── INotification.cs           # Interface: NotifyAsync(string message)
│   └── Manager/
│       ├── EmailNotification.cs       # Email implementation
│       ├── PushNotification.cs        # Push implementation
│       └── SmsNotification.cs         # SMS implementation
├── Properties/
│   └── launchSettings.json            # Default port: 7194
├── Program.cs                         # Host builder with keyed DI registration
├── host.json
└── KeyedService.csproj
```

## FAQ

**What are keyed services in .NET?**
Introduced in .NET 8, keyed services let you register several implementations of one interface under different keys (`AddKeyedScoped`, `AddKeyedSingleton`, `AddKeyedTransient`) and request a specific one with `[FromKeyedServices("key")]` or `GetRequiredKeyedService<T>("key")`.

**Do keyed services work in Azure Functions?**
Yes — this project uses them in the .NET 8 isolated worker model with constructor injection into function classes.

## Related Examples

- [MediatR and CQRS in Azure Functions](../Mediat/readme.md)
- [Azure Functions middleware](../Middleware/README.md)
- [Durable Functions orchestration](../Durable/readme.md)
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
