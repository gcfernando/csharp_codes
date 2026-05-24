# MediatR — Azure Functions Demo

An Azure Functions v4 solution on .NET 8 that demonstrates the **MediatR** pattern with a clean multi-layer architecture. The solution implements user creation and retrieval through CQRS commands and queries, wired with two MediatR pipeline behaviours.

## Architecture

```
HTTP Request
     │
     ▼
Mediat.AzureFunction     ← Azure Functions host (HTTP triggers)
     │  sends IRequest via IMediator
     ▼
Mediat.Business          ← Command/Query handlers
     │  calls repository
     ▼
Mediat.Data              ← In-memory repository implementation
     │  uses model
     ▼
Mediat.Model             ← User entity
```

Pipeline behaviours (Mediat.Infrastructure) wrap every handler call:

```
LoggingBehavior → PerformanceMonitoringBehavior → Handler
```

## Projects

| Project | Description |
|---|---|
| `Mediat.AzureFunction` | Azure Functions host — HTTP triggers for create and get user |
| `Mediat.Business` | `CreateUserCommand` + handler, `GetUserQuery` + handler |
| `Mediat.Data` | `IUserRepository` interface and in-memory `UserRepository` |
| `Mediat.Infrastructure` | `LoggingBehavior` and `PerformanceMonitoringBehavior` |
| `Mediat.Model` | `User` entity (`Id`, `Name`, `Email`) |

## HTTP Endpoints

| Function | Method | Route | Description |
|---|---|---|---|
| `create-user-function` | POST | `/api/user` | Creates a new user from JSON body |
| `get-user-function` | GET | `/api/user/{id}` | Retrieves a user by integer ID |

### Request Body (POST)

```json
{
  "name": "Alice",
  "email": "alice@example.com"
}
```

### Response (POST / GET)

```json
{
  "id": 1,
  "name": "Alice",
  "email": "alice@example.com"
}
```

## Pipeline Behaviours

| Behaviour | Description |
|---|---|
| `LoggingBehavior<TRequest, TResponse>` | Logs the request type name before and after each handler call |
| `PerformanceMonitoringBehavior<TRequest, TResponse>` | Logs a warning if handler execution exceeds 500 ms |

## Data Storage

`UserRepository` uses a static in-memory `List<User>`. IDs are assigned sequentially (count + 1). Data is not persisted between application restarts.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- A `local.settings.json` file in `Mediat.AzureFunction` (not committed to source control)

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

## NuGet Packages (Mediat.AzureFunction)

| Package | Version |
|---|---|
| `MediatR.Extensions.Microsoft.DependencyInjection` | 11.0.0 |
| `Microsoft.Azure.Functions.Worker` | 2.0.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.3.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 2.0.1 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.2 |
| `Newtonsoft.Json` | 13.0.3 |

## Running Locally

```bash
cd Mediat/Mediat.AzureFunction
func start --port 7094
```

## Usage

```bash
# Create a user
curl -X POST http://localhost:7094/api/user \
  -H "Content-Type: application/json" \
  -d '{"name": "Alice", "email": "alice@example.com"}'

# Retrieve a user
curl http://localhost:7094/api/user/1
```

## Project Structure

```
Mediat/
├── Mediat.AzureFunction/
│   ├── CreateUserFunction.cs          # POST /api/user
│   ├── GetUserFunction.cs             # GET /api/user/{id}
│   └── Program.cs                     # MediatR and DI registration
├── Mediat.Business/
│   ├── Commands/UserCommand/
│   │   ├── CreateUserCommand.cs
│   │   └── CreateUserCommandHandler.cs
│   └── Queries/UserQuery/
│       ├── GetUserQuery.cs
│       └── GetUserQueryHandler.cs
├── Mediat.Data/
│   ├── Contract/IUserRepository.cs
│   └── Repository/UserRepository.cs   # In-memory implementation
├── Mediat.Infrastructure/
│   ├── LoggingBehavior.cs
│   └── PerformanceMonitoringBehavior.cs
├── Mediat.Model/
│   └── User.cs                        # Id, Name, Email
└── Mediat.sln
```
