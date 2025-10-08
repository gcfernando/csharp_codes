using System.Buffers;
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
///   <item>Implements <see cref="ILogger"/> so it can be injected via DI.</item>
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
public sealed class BatchLogger : ILogger, IDisposable
{
    private readonly ILogger _logger;                        // Underlying sink (console, file, etc.)
    private readonly Channel<LogEntry> _channel;             // Bounded channel buffer for logs
    private readonly CancellationTokenSource _cts = new();   // Cancellation token for background worker
    private readonly Task _backgroundTask;                   // Background task that drains logs
    private readonly List<LogEntry> _buffer;                 // Reused buffer to reduce allocations

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
        _buffer = new List<LogEntry>(batchSize);

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
    // ILogger Implementation
    // ----------------------------------------------------------------

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId,
        TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter != null ? formatter(state, exception) : state?.ToString() ?? "N/A";

        // Use ArrayPool for args when logging structured state (optimization)
        var args = Array.Empty<object>();
        if (state is IEnumerable<KeyValuePair<string, object>> kvps)
        {
            args = [.. kvps.Select(kv => kv.Value ?? "N/A")];
        }

        _ = _channel.Writer.TryWrite(new LogEntry(logLevel, message, exception, args));
    }

    // ----------------------------------------------------------------
    // 🚀 Shorthand wrappers for log levels (with/without exception args)
    // ----------------------------------------------------------------

    public void LogTrace(string message, Exception exception = null, params object[] args) =>
        LogInternal(LogLevel.Trace, message, exception, args);

    public void LogDebug(string message, Exception exception = null, params object[] args) =>
        LogInternal(LogLevel.Debug, message, exception, args);

    public void LogInformation(string message, Exception exception = null, params object[] args) =>
        LogInternal(LogLevel.Information, message, exception, args);

    public void LogWarning(string message, Exception exception = null, params object[] args) =>
        LogInternal(LogLevel.Warning, message, exception, args);

    public void LogError(string message, Exception exception = null, params object[] args) =>
        LogInternal(LogLevel.Error, message, exception, args);

    public void LogCritical(string message, Exception exception = null, params object[] args) =>
        LogInternal(LogLevel.Critical, message, exception, args);

    // ----------------------------------------------------------------
    // 🛠 Exception helpers (mirror ExLogger API, but allow per-instance formatter)
    // ----------------------------------------------------------------

    /// <summary>
    /// Logs an exception at <see cref="LogLevel.Error"/> with formatted details.
    /// </summary>
    public void LogErrorException(Exception ex, string title = "System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!IsEnabled(LogLevel.Error))
        {
            return;
        }

        var formatter = ExceptionFormatter ?? ExLogger.ExceptionFormatter;
        var msg = formatter(ex, title, moreDetailsEnabled);
        LogInternal(LogLevel.Error, msg, ex);
    }

    /// <summary>
    /// Logs an exception at <see cref="LogLevel.Critical"/> with formatted details.
    /// </summary>
    public void LogCriticalException(Exception ex, string title = "Critical System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!IsEnabled(LogLevel.Critical))
        {
            return;
        }

        var formatter = ExceptionFormatter ?? ExLogger.ExceptionFormatter;
        var msg = formatter(ex, title, moreDetailsEnabled);
        LogInternal(LogLevel.Critical, msg, ex);
    }

    // ----------------------------------------------------------------
    // 🔎 Scope support (passthrough to underlying ILogger)
    // ----------------------------------------------------------------

    /// <inheritdoc/>
    public IDisposable BeginScope<TState>(TState state) =>
        _logger.BeginScope(state) ?? NullScope.Instance;

    /// <summary>
    /// Begins a structured logging scope with a single key-value pair.
    /// Useful for correlating logs (e.g., RequestId, UserId).
    /// </summary>
    public IDisposable BeginScope(string key, object value) =>
        _logger.ExBeginScope(key, value);

    /// <summary>
    /// Begins a structured logging scope with multiple key-value pairs.
    /// Optimized for small dictionaries (≤4 items) to reduce allocations.
    /// </summary>
    public IDisposable BeginScope(IDictionary<string, object> context) =>
        _logger.ExBeginScope(context);

    // ----------------------------------------------------------------
    // Core: enqueue + background flush loop
    // ----------------------------------------------------------------

    /// <summary>
    /// Internal enqueue helper for shorthand methods.
    /// </summary>
    private void LogInternal(LogLevel level, string message, Exception exception, params object[] args)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        _ = _channel.Writer.TryWrite(
            new LogEntry(level, message ?? "N/A", exception, args ?? Array.Empty<object>())
        );
    }

    /// <summary>
    /// Background loop: drains channel, flushes logs in batches or at interval.
    /// </summary>
    private async Task ProcessAsync(int batchSize, TimeSpan flushInterval, CancellationToken token)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var entry))
                {
                    _buffer.Add(entry);
                    if (_buffer.Count >= batchSize)
                    {
                        Flush(_buffer);
                    }
                }

                if (_buffer.Count > 0)
                {
                    await Task.Delay(flushInterval, token).ConfigureAwait(false);
                    Flush(_buffer);
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
        buffer.Clear();
    }

    /// <summary>
    /// Manually flushes buffered entries immediately.
    /// </summary>
    public async Task FlushAsync(CancellationToken token = default)
    {
        while (_channel.Reader.TryRead(out var entry))
        {
            token.ThrowIfCancellationRequested();
            _buffer.Add(entry);
        }

        if (_buffer.Count > 0)
        {
            Flush(_buffer);
        }

        await Task.Yield();
    }

    // ----------------------------------------------------------------
    // Cleanup
    // ----------------------------------------------------------------

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _backgroundTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            ExLogger.Log(_logger, LogLevel.Error, "BatchLogger background task error during Dispose", ex);
        }

        while (_channel.Reader.TryRead(out var entry))
        {
            _buffer.Add(entry);
        }
        if (_buffer.Count > 0)
        {
            Flush(_buffer);
        }

        _cts.Dispose();
    }

    /// <summary>
    /// Lightweight struct representing a buffered log entry.
    /// </summary>
    private readonly record struct LogEntry(LogLevel Level, string Message, Exception Exception, object[] Args);
}