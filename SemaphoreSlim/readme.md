# SemaphoreSlim Example in C# — Throttle Concurrent Async Downloads (.NET 8)

A .NET 8 console application demonstrating how to **limit concurrency with `SemaphoreSlim` in C#** — throttling the number of asynchronous operations that run at the same time, a common need when calling APIs, downloading files, or querying databases in parallel. The `FileDownloader` class downloads up to N files simultaneously from a queue while respecting the concurrency limit.

## How It Works

`FileDownloader` accepts a list of file names and a maximum concurrency count. Internally it uses:

- **`ConcurrentQueue<string>`** — thread-safe queue of pending file names
- **`SemaphoreSlim`** — limits the number of downloads running at the same time

`StartDownloadAsync` seeds the initial wave of concurrent tasks (up to the semaphore capacity). Each `DownloadFileAsync` call acquires the semaphore, simulates a download with a random delay (500–2000 ms), releases the semaphore, then immediately picks the next file from the queue — keeping all slots busy until the queue is empty.

The class implements `IDisposable` (with a finaliser) to properly release the `SemaphoreSlim`.

## Demo

The `Main` method creates a downloader for 15 files with a maximum of 3 concurrent downloads:

```csharp
using var downloader = new FileDownloader(files, 3);
await downloader.StartDownloadAsync();
```

Console output shows which files are downloading and when each completes, followed by `All files have been downloaded.`

## Known behaviour: only two downloads run at once

Although the downloader is created with a limit of 3, a test run of the demo (on .NET 10) never showed more than two downloads in progress at the same time. The cause is the start-up loop:

```csharp
while (tasks.Count < _semaphore.CurrentCount && _fileQueue.TryDequeue(out var file))
```

Each `DownloadFileAsync` call acquires the semaphore synchronously before returning, so `CurrentCount` drops while `tasks.Count` rises: after two tasks, `2 < 1` is false and the loop stops. Each started task then processes the rest of the queue one file at a time, so the effective concurrency is 2.

Comparing against the configured limit instead of the live count — for example, storing `maxConcurrentDownloads` in a field and using `tasks.Count < _maxConcurrency` — starts the intended number of workers.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Build and Run

```bash
cd SemaphoreSlim
dotnet run
```

## Project Structure

```
SemaphoreSlim/
├── Program.cs         # FileDownloader class + Main entry point
├── Test.csproj
└── Test.sln
```

## Related Examples

- [Capturing all exceptions from Task.WhenAll](../TaskErrors/readme.md)
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
