# FakeLogger Testing Demo

A .NET 8 solution demonstrating how to use `FakeLogger<T>` from `Microsoft.Extensions.Diagnostics.Testing` to write unit tests for code that depends on `ILogger`.

## Overview

The project shows how to verify logging behaviour in tests without mocking or capturing log output manually. `FakeLogger<T>` records all log calls and exposes them through `LatestRecord`, making it straightforward to assert on log messages and log levels.

## Projects

| Project | Description |
|---|---|
| `Fake.Data` | Contains the `Calculate` class with a `Divide` method that logs results and errors |
| `Fake.Test` | MSTest project with two tests validating the logging behaviour of `Calculate` |

## The Class Under Test

`Calculate.Divide(int dividend, int divisor)` performs integer division and uses `ILogger<Program>` to:

- Log the inputs and result on success
- Log an `Error`-level entry with the exception on divide-by-zero

```csharp
public int? Divide(int dividend, int divisor)
```

Returns `null` if division fails (e.g. division by zero).

## Tests

| Test | What It Verifies |
|---|---|
| `CanDevide` | After `Divide(10, 2)`, the latest log message contains `"Dividing answer 5"` |
| `CanLogError` | After `Divide(10, 0)`, the latest log record has `LogLevel.Error` |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## NuGet Packages

**Fake.Data**

| Package | Version |
|---|---|
| `Microsoft.Extensions.Logging` | 8.0.0 |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.1 |
| `Microsoft.Extensions.Logging.Console` | 8.0.0 |

**Fake.Test**

| Package | Version |
|---|---|
| `Microsoft.Extensions.Diagnostics.Testing` | 8.5.0 |
| `Microsoft.NET.Test.Sdk` | 17.9.0 |
| `MSTest.TestAdapter` | 3.3.1 |
| `MSTest.TestFramework` | 3.3.1 |
| `coverlet.collector` | 6.0.2 |

## Build and Test

```bash
cd Fake
dotnet build
dotnet test
```

## Project Structure

```
Fake/
├── Fake.Data/
│   ├── Calculate.cs        # Class under test — Divide method with ILogger
│   └── Fake.Data.csproj
├── Fake.Test/
│   ├── CalculateTest.cs    # Two MSTest tests using FakeLogger<T>
│   └── Fake.Test.csproj
└── Fake.sln
```
