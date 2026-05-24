# ExLogger — High-Performance .NET Logging Engine

A three-project solution containing a custom high-performance logging library (`ExLogger`), an Azure Functions v4 host that demonstrates it in action, and a BenchmarkDotNet suite that compares `ExLogger` against the standard `ILogger` interface.

**Developer:** Gehan Fernando | **Framework:** .NET 8.0 | **Test Tool:** BenchmarkDotNet

## Projects

| Project | Description |
|---|---|
| `LogFunction.Logger` | The `ExLogger` static class — the logging engine |
| `LogFunction.Core` | Azure Functions v4 host with an HTTP function that runs the `ExLogger` demo |
| `LogFunction.Benchmark` | BenchmarkDotNet benchmarks comparing `ExLogger` to `ILogger` |

## LogFunction.Logger — ExLogger

`ExLogger` is a zero-allocation, high-throughput logging utility built on `Microsoft.Extensions.Logging`. Key design decisions:

- **Pre-compiled log delegates** via `LoggerMessage.Define` — eliminates per-call delegate allocation
- **Level-indexed dispatch array** — `O(1)` log-level routing with no switch overhead
- **Cached EventIds** — no `new EventId(...)` allocation at call sites
- **Thread-local UTC timestamp cache** updated every millisecond — avoids `DateTime.UtcNow` formatting overhead
- **`StringBuilder` pool** via `Microsoft.Extensions.ObjectPool` — reused buffers for exception formatting
- **Allocation-free scopes** — `SingleScope`, `SmallScopeWrapper` (up to 4 keys), and `ScopeWrapper` structs
- **Pluggable `ExceptionFormatter`** — replace exception formatting globally without subclassing

### Extension Methods

| Method | Description |
|---|---|
| `ExLogTrace` | Logs at Trace level |
| `ExLogDebug` | Logs at Debug level |
| `ExLogInformation` | Logs at Information level |
| `ExLogWarning` | Logs at Warning level |
| `ExLogError` | Logs at Error level (with or without exception) |
| `ExLogCritical` | Logs at Critical level (with or without exception) |
| `ExLogErrorException` | Formats and logs a full exception at Error level |
| `ExLogCriticalException` | Formats and logs a full exception at Critical level |
| `ExBeginScope(key, value)` | Starts a single-key log scope |
| `ExBeginScope(dictionary)` | Starts a multi-key log scope |

## LogFunction.Core — Azure Function

An HTTP-triggered function (`logger-test`) that exercises every `ExLogger` feature: all log levels, structured logging with parameters, exception logging, scoped logging, and the generic `ExLogger.Log()` helper.

### Running Locally

```bash
cd LogFunction/LogFunction.Core
func start
```

Trigger the demo:

```bash
curl http://localhost:7071/api/logger-test
```

Returns:

```json
{
  "Message": "Logger demo completed. Check console or Application Insights logs.",
  "Timestamp": "..."
}
```

## LogFunction.Benchmark — Benchmarks

### Benchmark Classes

| Class | What It Measures |
|---|---|
| `LoggerComparisonBenchmarks` | Head-to-head: Information, Warning, Error, Critical, Trace, and 1 000-iteration throughput |
| `ThroughputBenchmarks` | Parallel throughput at 1, 2, 4, 8, and 16 threads |
| `ScopeBenchmarks` | Single-key and multi-key scope overhead |
| `ExceptionFormatterBenchmarks` | Simple vs. deep exception formatting cost |
| `ExLoggerAllMethodsBenchmarks` | All individual `ExLogger` methods in isolation |
| `AsyncBenchmarks` | `FlushAsync` and async sink filter overhead |
| `InitializationBenchmarks` | Static initialisation cost |
| `ExceptionBenchmarks` | Exception creation and capture overhead |

### Running Benchmarks

```bash
cd LogFunction/LogFunction.Benchmark
dotnet run -c Release
```

## Performance Summary

| Scenario | ILogger (ns) | ExLogger (ns) | Speed Gain |
|---|---:|---:|---:|
| LogInformation | 35.50 | 9.89 | 3.6× faster |
| LogWarning | 31.59 | 6.11 | 5.2× faster |
| LogTrace | 8.86 | 1.04 | 8.5× faster |
| HighThroughput (1 000 logs) | 22,288 | 6,846 | 3.3× faster |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (for `LogFunction.Core` only)

## NuGet Packages

**LogFunction.Logger**

| Package | Version |
|---|---|
| `Microsoft.Extensions.Logging.Abstractions` | 9.0.9 |
| `Microsoft.Extensions.ObjectPool` | 9.0.9 |

**LogFunction.Core**

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.1.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 2.0.2 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.5 |
| `Microsoft.ApplicationInsights.WorkerService` | 2.23.0 |
| `Microsoft.Azure.Functions.Worker.ApplicationInsights` | 2.0.0 |

## Project Structure

```
LogFunction/
├── LogFunction.Logger/
│   ├── ExLogger.cs          # Main logging engine
│   └── NullScope.cs         # IDisposable no-op scope
├── LogFunction.Core/
│   ├── TrackAllFunction.cs  # HTTP function running the ExLogger demo
│   └── Program.cs           # Host builder, logging config, custom exception formatter
├── LogFunction.Benchmark/
│   ├── LoggerComparisonBenchmarks.cs
│   ├── ThroughputBenchmarks.cs
│   ├── ScopeBenchmarks.cs
│   ├── ExceptionFormatterBenchmarks.cs
│   ├── ExLoggerAllMethodsBenchmarks.cs
│   └── Program.cs           # BenchmarkDotNet entry point
└── LogFunction.sln
```
