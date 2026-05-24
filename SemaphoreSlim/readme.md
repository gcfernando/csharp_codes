# SemaphoreSlim — Concurrent File Downloader

A .NET 8 console application demonstrating how to use `SemaphoreSlim` to limit the number of concurrent asynchronous operations. The `FileDownloader` class downloads up to N files simultaneously from a queue while respecting the concurrency limit.

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

Console output shows which files are downloading and when each completes.

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
