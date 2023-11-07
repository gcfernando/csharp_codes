using System.Collections.Concurrent;

namespace Test;
public class FileDownloader : IDisposable
{
    private readonly ConcurrentQueue<string> _fileQueue;
    private readonly SemaphoreSlim _semaphore;

    private bool _disposed; // To track disposal status

    public FileDownloader(IEnumerable<string> filesToDownload, int maxConcurrentDownloads)
    {
        _fileQueue = new ConcurrentQueue<string>(filesToDownload);
        _semaphore = new SemaphoreSlim(maxConcurrentDownloads, maxConcurrentDownloads);
    }

    public async Task StartDownloadAsync()
    {
        var tasks = new List<Task>();
        while (tasks.Count < _semaphore.CurrentCount && _fileQueue.TryDequeue(out var file))
        {
            tasks.Add(DownloadFileAsync(file));
        }
        await Task.WhenAll(tasks);
    }

    private async Task DownloadFileAsync(string file)
    {
        await _semaphore.WaitAsync();
        try
        {
            Console.WriteLine($"Downloading {file}...");
            await Task.Delay(new Random().Next(500, 2000)); // Simulate file download
            Console.WriteLine($"Downloaded {file}.");
        }
        finally
        {
            _semaphore.Release();
            if (_fileQueue.TryDequeue(out var nextFile))
            {
                await DownloadFileAsync(nextFile);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _semaphore?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~FileDownloader()
    {
        Dispose(false);
    }
}

public static class Program
{
    public static async Task Main()
    {
        var files = new List<string>
        {
            "file1.txt", "file2.txt", "file3.txt", "file4.txt", "file5.txt",
            "file6.txt", "file7.txt", "file8.txt", "file9.txt", "file10.txt",
            "file11.txt", "file12.txt", "file13.txt", "file14.txt", "file15.txt"
        };

        try
        {
            using var downloader = new FileDownloader(files, 3); // Download 3 files at a time
            await downloader.StartDownloadAsync();

            Console.WriteLine("All files have been downloaded.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An unexpected error occurred: {ex.Message}");
        }
    }
}