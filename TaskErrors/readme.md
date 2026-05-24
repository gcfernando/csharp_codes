# Task Errors — Capturing All Exceptions from Task.WhenAll

A .NET 8 console application demonstrating the correct pattern for capturing **all** exceptions thrown by multiple concurrent tasks when using `Task.WhenAll`.

## The Problem

When `Task.WhenAll` is awaited and multiple tasks throw, only the **first** exception is rethrown into the catch block. The remaining exceptions are silently discarded if you only inspect the caught `Exception`.

## The Solution

After catching, iterate over each task in your list and collect their `Exception.InnerExceptions`:

```csharp
catch (Exception ex)
{
    var exceptions = tasks.SelectMany(
        task => task.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>());

    foreach (var exception in exceptions)
    {
        Console.WriteLine(exception.Message);
    }
}
```

## Demo

The application runs two async methods concurrently:

| Method | Delay | Exception Message |
|---|---|---|
| `MethodOneAsync` | 1 second | `"Custom Error Method One"` |
| `MethodTwoAsync` | 2 seconds | `"Custom Error Method Two"` |

Both tasks are stored in a `List<Task>` before `Task.WhenAll` is awaited. After the catch block, the code collects inner exceptions from all tasks and prints both error messages.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Build and Run

```bash
cd TaskErrors
dotnet run
```

## Expected Output

```
Custom Error Method One
Custom Error Method Two
```

## Project Structure

```
TaskErrors/
├── Program.cs          # Two throwing async methods + WhenAll error capture pattern
├── TaskErrors.csproj
└── TaskErrors.sln
```
