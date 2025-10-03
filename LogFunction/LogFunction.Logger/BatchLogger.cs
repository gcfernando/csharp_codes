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
///   <item>Supports per-instance <see cref="ExceptionFormatter"/> (defaults to <see cref="ExLogger.ExceptionFormatter"/>).</item>
///   <item>Supports <see cref="BeginScope"/> for contextual logging (single or multiple key-value pairs).</item>
/// </list>
/// </para>
/// <para>
/// ⚠️ NOTE: In production, register as a Singleton (e.g., via DI).
/// In short-lived contexts (e.g., Azure Functions), always wrap in <c>using</c> so <see cref="Dispose"/> flushes remaining logs.
/// </para>
/// </summary>
public sealed class BatchLogger : IDisposable
{
    private readonly ILogger _logger;                        // Underlying sink (console, file, etc.)
    private readonly Channel<LogEntry> _channel;             // Bounded channel buffer for logs
    private readonly CancellationTokenSource _cts = new();   // Cancellation token for background worker
    private readonly Task _backgroundTask;                   // Background task that drains logs

    /// <summary>
    /// Formatter used for exception logs in this <see cref="BatchLogger"/> instance.
    /// Defaults to <see cref="ExLogger.ExceptionFormatter"/>.
    /// </summary>
    public Func<Exception, string, bool, string> ExceptionFormatter { get; set; }
        = ExLogger.ExceptionFormatter;

    /// <summary>
    /// Creates a new instance of <see cref="BatchLogger"/>.
    /// </summary>
    /// <param name="logger">The underlying <see cref="ILogger"/> sink (e.g., ConsoleLogger, Serilog adapter).</param>
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

        // Create bounded channel (drops oldest entries if full to prevent blocking producers)
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = false, // multiple producers can log concurrently
            SingleReader = true,  // one background consumer
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // Start background consumer task to drain logs asynchronously
        _backgroundTask = Task.Run(() =>
            ProcessAsync(batchSize, flushInterval ?? TimeSpan.FromMilliseconds(200), _cts.Token));
    }

    // ----------------------------------------------------------------
    // 🚀 Shorthand wrappers for log levels (with/without exception args)
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
    // 🛠 Exception helpers (mirror ExLogger API, but allow per-instance formatter)
    // ----------------------------------------------------------------

    /// <summary>
    /// Logs an exception at <see cref="LogLevel.Error"/> with formatted details.
    /// </summary>
    public void LogErrorException(Exception ex, string title = "System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!_logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        // Use instance-level formatter (falls back to ExLogger's default if unchanged)
        var formatter = ExceptionFormatter ?? ExLogger.ExceptionFormatter;
        var msg = formatter(ex, title, moreDetailsEnabled);
        Log(LogLevel.Error, msg, ex);
    }

    /// <summary>
    /// Logs an exception at <see cref="LogLevel.Critical"/> with formatted details.
    /// </summary>
    public void LogCriticalException(Exception ex, string title = "Critical System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!_logger.IsEnabled(LogLevel.Critical))
        {
            return;
        }

        var formatter = ExceptionFormatter ?? ExLogger.ExceptionFormatter;
        var msg = formatter(ex, title, moreDetailsEnabled);
        Log(LogLevel.Critical, msg, ex);
    }

    // ----------------------------------------------------------------
    // 🔎 Scope support (passthrough to underlying ILogger)
    // ----------------------------------------------------------------

    /// <summary>
    /// Begins a structured logging scope with a single key-value pair.
    /// Useful for correlating logs (e.g., RequestId, UserId).
    /// </summary>
    public IDisposable BeginScope(string key, object value) =>
        ExLogger.BeginScope(_logger, key, value);

    /// <summary>
    /// Begins a structured logging scope with multiple key-value pairs.
    /// Optimized for small dictionaries (≤4 items) to reduce allocations.
    /// </summary>
    public IDisposable BeginScope(IDictionary<string, object> context) =>
        ExLogger.BeginScope(_logger, context);

    // ----------------------------------------------------------------
    // Core: enqueue + background flush loop
    // ----------------------------------------------------------------

    /// <summary>
    /// Queues a log entry (non-blocking).
    /// If buffer is full, the oldest entry is dropped.
    /// </summary>
    private void Log(LogLevel level, string message, Exception exception = null, params object[] args)
    {
        if (!_logger.IsEnabled(level))
        {
            return;
        }

        // TryWrite is non-blocking; drops if full (due to DropOldest mode)
        _ = _channel.Writer.TryWrite(
            new LogEntry(level, message ?? "N/A", exception, args ?? Array.Empty<object>())
        );
    }

    /// <summary>
    /// Background loop: drains channel, flushes logs in batches or at interval.
    /// </summary>
    private async Task ProcessAsync(int batchSize, TimeSpan flushInterval, CancellationToken token)
    {
        var buffer = new List<LogEntry>(batchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                // Drain available entries quickly
                while (_channel.Reader.TryRead(out var entry))
                {
                    buffer.Add(entry);

                    // Flush immediately if batch is full
                    if (buffer.Count >= batchSize)
                    {
                        Flush(buffer);
                    }
                }

                // Flush periodically even if batch not full
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
    /// Writes buffered entries to the underlying sink via <see cref="ExLogger"/>.
    /// </summary>
    private void Flush(List<LogEntry> buffer)
    {
        foreach (var entry in buffer)
        {
            ExLogger.Log(_logger, entry.Level, entry.Message, entry.Exception, entry.Args);
        }

        buffer.Clear(); // Reuse the list buffer
    }

    /// <summary>
    /// Manually flushes buffered entries immediately.
    /// Useful for short-lived contexts (Azure Functions) to ensure logs are written
    /// before returning a response, without waiting for <see cref="Dispose"/>.
    /// </summary>
    public async Task FlushAsync(CancellationToken token = default)
    {
        var buffer = new List<LogEntry>();

        // Drain channel synchronously until empty
        while (_channel.Reader.TryRead(out var entry))
        {
            token.ThrowIfCancellationRequested();
            buffer.Add(entry);
        }

        if (buffer.Count > 0)
        {
            Flush(buffer);
        }

        // Yield back so background worker can finish pending writes
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
            // Ensure background task finishes
            _backgroundTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            // Should not happen, log synchronously if it does
            ExLogger.Log(_logger, LogLevel.Error, "BatchLogger background task error during Dispose", ex);
        }

        // Final flush of anything left in channel
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