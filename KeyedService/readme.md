# Keyed Services — Azure Functions Demo

An Azure Functions v4 demo on .NET 8 that demonstrates **keyed dependency injection** (`AddKeyedScoped` / `[FromKeyedServices]`). Three HTTP endpoints each receive a different implementation of `INotification` injected by its registered key.

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

## Project Structure

```
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
