# Task.WhenAll Exception Handling in C# — Capturing All Exceptions (.NET 8)

A .NET 8 console application demonstrating how to **catch every exception from `Task.WhenAll` in C#**, not just the first one, when several concurrent tasks fail.

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

The expected output above was confirmed by running the project.

## FAQ

**Why does `await Task.WhenAll(...)` only throw one exception?**
`Task.WhenAll` returns a task whose `Exception` is an `AggregateException` containing every failure, but `await` unwraps it and rethrows only the first inner exception. Keeping a reference to the tasks (or to the `WhenAll` task) lets you read all of them afterwards.

**Is there a shorter alternative?**
You can store the `WhenAll` task in a variable, await it inside `try`, and in the `catch` read `whenAllTask.Exception.InnerExceptions` — it contains the same exceptions as collecting them from each task.

## Related Examples

- [Unit testing ILogger with FakeLogger](../Fake/readme.md)
- [SemaphoreSlim concurrency example](../SemaphoreSlim/readme.md)
- [Back to all C# and .NET code examples](../README.md)
