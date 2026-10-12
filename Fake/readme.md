# Unit Testing ILogger with FakeLogger in C# — .NET 8 MSTest Example

A .NET 8 solution demonstrating how to **unit test logging in C#** with `FakeLogger<T>` from the `Microsoft.Extensions.Diagnostics.Testing` package. It shows how to assert on log messages and log levels written through `ILogger<T>` — without a mocking library, custom test doubles, or capturing console output.

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

`Fake.Data` also contains a small console `Program.cs` that runs the calculator with a console logger, so you can see the same log messages outside the tests.

## FAQ

**Why use `FakeLogger<T>` instead of mocking `ILogger`?**
`ILogger.Log` is a generic method that is awkward to verify with mocking libraries. `FakeLogger<T>` is a real `ILogger<T>` implementation that records every entry, so tests can read `LatestRecord` and assert on the message, level, and exception directly.

**Does `FakeLogger` work with xUnit or NUnit?**
Yes. It comes from the `Microsoft.Extensions.Diagnostics.Testing` package and does not depend on a test framework; this demo happens to use MSTest.

## Related Examples

- [Nilog](https://github.com/gcfernando/nilog) — zero-allocation `ILogger` extensions for high-performance logging
- [Capturing all exceptions from Task.WhenAll](../TaskErrors/readme.md)
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
