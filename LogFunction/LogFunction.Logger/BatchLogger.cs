using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace LogFunction.Logger;

/// <summary>
/// <b>BatchLogger</b>: A high-throughput, batching wrapper for <see cref="ExLogger"/>.
/// <para>
/// Unlike <see cref="ExLogger"/> which logs immediately, <b>BatchLogger</b> is designed
/// for scenarios with very high log volumes where direct sinks (Console, File, AppInsights)
/// can become the bottleneck.
/// </para>
/// <para>
/// Features:
/// <list type="bullet">
///   <item>Non-blocking writes via <see cref="Channel{T}"/> (drops oldest entries if full).</item>
///   <item>Configurable buffer <c>capacity</c>, <c>batchSize</c>, and <c>flushInterval</c>.</item>
///   <item>Background worker task flushes buffered logs asynchronously.</item>
///   <item>All logs are forwarded to <see cref="ExLogger"/> for fast delegates,
///         structured logging, exception formatting, and scope support.</item>
/// </list>
/// </para>
/// <para>
/// ⚠️ NOTE: In production, register as a Singleton (e.g., via DI).
/// In short-lived contexts (e.g., Azure Functions), always wrap in <c>using</c> so <see cref="Dispose"/> flushes remaining logs.
/// </para>
/// </summary>
public sealed class BatchLogger : IDisposable
{
    private readonly ILogger _logger;                  // Underlying sink
    private readonly Channel<LogEntry> _channel;       // Buffered channel
    private readonly CancellationTokenSource _cts = new(); // Cancellation for background worker
    private readonly Task _backgroundTask;             // Background flushing task

    /// <summary>
    /// Creates a new instance of <see cref="BatchLogger"/>.
    /// </summary>
    /// <param name="logger">The underlying <see cref="ILogger"/> sink.</param>
    /// <param name="capacity">Maximum number of buffered log entries (default: 10,000).</param>
    /// <param name="batchSize">Maximum number of entries flushed in one batch (default: 100).</param>
    /// <param name="flushInterval">Time-based flush trigger if batch is not full (default: 200ms).</param>
    public BatchLogger(
        ILogger logger,
        int capacity = 10_000,
        int batchSize = 100,
        TimeSpan? flushInterval = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create bounded channel (dropping oldest entries when full prevents blocking)
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = false, // multiple producers can log concurrently
            SingleReader = true,  // single background consumer
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // Start background consumer task that drains logs
        _backgroundTask = Task.Run(() =>
            ProcessAsync(batchSize, flushInterval ?? TimeSpan.FromMilliseconds(200), _cts.Token));
    }

    // ----------------------------------------------------------------
    // 🚀 Shorthand wrappers for log levels (with/without args)
    // ----------------------------------------------------------------
    public void LogTrace(string message, Exception exception, params object[] args) =>
        Log(LogLevel.Trace, message, exception, args);

    public void LogTrace(string message) => Log(LogLevel.Trace, message, null);

    public void LogDebug(string message, Exception exception, params object[] args) =>
        Log(LogLevel.Debug, message, exception, args);

    public void LogDebug(string message) => Log(LogLevel.Debug, message, null);

    public void LogInformation(string message, Exception exception, params object[] args) =>
        Log(LogLevel.Information, message, exception, args);

    public void LogInformation(string message) => Log(LogLevel.Information, message, null);

    public void LogWarning(string message, Exception exception, params object[] args) =>
        Log(LogLevel.Warning, message, exception, args);

    public void LogWarning(string message) => Log(LogLevel.Warning, message, null);

    public void LogError(string message, Exception exception, params object[] args) =>
        Log(LogLevel.Error, message, exception, args);

    public void LogError(string message) => Log(LogLevel.Error, message, null);

    public void LogCritical(string message, Exception exception, params object[] args) =>
        Log(LogLevel.Critical, message, exception, args);

    public void LogCritical(string message) => Log(LogLevel.Critical, message, null);

    // ----------------------------------------------------------------
    // 🛠 Exception helpers (mirrors ExLogger API)
    // ----------------------------------------------------------------
    /// <summary>
    /// Logs an exception at Error level with formatted details.
    /// </summary>
    public void LogErrorException(Exception ex, string title = "System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!_logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        var msg = ExLogger.ExceptionFormatter(ex, title, moreDetailsEnabled);
        Log(LogLevel.Error, msg, ex);
    }

    /// <summary>
    /// Logs an exception at Critical level with formatted details.
    /// </summary>
    public void LogCriticalException(Exception ex, string title = "Critical System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!_logger.IsEnabled(LogLevel.Critical))
        {
            return;
        }

        var msg = ExLogger.ExceptionFormatter(ex, title, moreDetailsEnabled);
        Log(LogLevel.Critical, msg, ex);
    }

    // ----------------------------------------------------------------
    // Core: enqueue + background flush loop
    // ----------------------------------------------------------------
    /// <summary>
    /// Queues a log entry (non-blocking).
    /// If buffer is full, oldest entry is dropped.
    /// </summary>
    private void Log(LogLevel level, string message, Exception exception = null, params object[] args)
    {
        if (!_logger.IsEnabled(level))
        {
            return;
        }

        _ = _channel.Writer.TryWrite(
            new LogEntry(level, message ?? "N/A", exception, args ?? Array.Empty<object>())
        );
    }

    /// <summary>
    /// Background loop: drains channel, flushes logs in batches or on interval.
    /// </summary>
    private async Task ProcessAsync(int batchSize, TimeSpan flushInterval, CancellationToken token)
    {
        var buffer = new List<LogEntry>(batchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                // Drain available entries
                while (_channel.Reader.TryRead(out var entry))
                {
                    buffer.Add(entry);

                    // Flush immediately if batch size reached
                    if (buffer.Count >= batchSize)
                    {
                        Flush(buffer);
                    }
                }

                // Flush on interval if batch not yet full
                if (buffer.Count > 0)
                {
                    await Task.Delay(flushInterval, token).ConfigureAwait(false);
                    Flush(buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <summary>
    /// Writes buffered entries using <see cref="ExLogger"/>.
    /// </summary>
    private void Flush(List<LogEntry> buffer)
    {
        foreach (var entry in buffer)
        {
            ExLogger.Log(_logger, entry.Level, entry.Message, entry.Exception, entry.Args);
        }
        buffer.Clear(); // reuse the buffer
    }

    /// <summary>
    /// Manually flushes buffered entries immediately.
    /// Useful for short-lived contexts (Azure Functions) to ensure logs are written
    /// before returning a response, without waiting for Dispose().
    /// </summary>
    public async Task FlushAsync(CancellationToken token = default)
    {
        var buffer = new List<LogEntry>();

        // Drain channel until empty, respecting cancellation
        while (_channel.Reader.TryRead(out var entry))
        {
            token.ThrowIfCancellationRequested();
            buffer.Add(entry);
        }

        if (buffer.Count > 0)
        {
            Flush(buffer);
        }

        // Yield back to scheduler to let background worker finish any pending writes
        await Task.Yield();
    }

    // ----------------------------------------------------------------
    // Cleanup
    // ----------------------------------------------------------------
    /// <summary>
    /// Stops background task and flushes remaining logs synchronously.
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            // Wait for background task to exit cleanly
            _backgroundTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected shutdown
        }
        catch (Exception ex)
        {
            // Should not happen, but log synchronously if it does
            ExLogger.Log(_logger, LogLevel.Error, "BatchLogger background task error during Dispose", ex);
        }

        // Final synchronous flush of any remaining logs still in channel
        var buffer = new List<LogEntry>();
        while (_channel.Reader.TryRead(out var entry))
        {
            buffer.Add(entry);
        }
        if (buffer.Count > 0)
        {
            Flush(buffer);
        }

        _cts.Dispose();
    }

    /// <summary>
    /// Lightweight struct representing a buffered log entry.
    /// </summary>
    private readonly record struct LogEntry(LogLevel Level, string Message, Exception Exception, object[] Args);
}