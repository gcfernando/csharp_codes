using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Collections;

/*
 * Developer ::> Gehan Fernando
 * Date      ::> 15-Sep-2025
*/

namespace LogFunction.Logger;

/// <summary>
/// A high-performance, centralized logging utility that provides:
/// - Strongly typed and consistent logging methods across all <see cref="LogLevel"/> values.
/// - Predefined delegates for zero-allocation logging when no formatting arguments are provided.
/// - Cached <see cref="EventId"/> instances for structured logging.
/// - Exception logging with pooled <see cref="StringBuilder"/> to minimize GC pressure.
/// - Scope helpers to attach contextual information (e.g., RequestId, UserId).
///
/// <para>
/// This utility is designed to maximize logging performance and consistency across an application,
/// while minimizing memory allocations and providing convenient APIs for structured and contextual logging.
/// </para>
/// </summary>
public static class ExLogger
{
    #region Predefined Delegates for Performance

    // These delegates are compiled at startup and reused.
    // They eliminate allocations that would normally occur
    // when calling ILogger.Log with message templates.
    private static readonly Action<ILogger, string, Exception> _trace =
        LoggerMessage.Define<string>(LogLevel.Trace, new EventId(0, "TraceEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _debug =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "DebugEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _info =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "InformationEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _warn =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, "WarningEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _error =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ErrorEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _critical =
        LoggerMessage.Define<string>(LogLevel.Critical, new EventId(5, "CriticalEvent"), "{Message}");

    /// <summary>
    /// Lookup table mapping <see cref="LogLevel"/> → predefined delegate.
    /// Used for the fast path (no message formatting arguments).
    /// </summary>
    private static readonly Dictionary<LogLevel, Action<ILogger, string, Exception>> _delegates = new()
    {
        [LogLevel.Trace] = _trace,
        [LogLevel.Debug] = _debug,
        [LogLevel.Information] = _info,
        [LogLevel.Warning] = _warn,
        [LogLevel.Error] = _error,
        [LogLevel.Critical] = _critical
    };

    #endregion Predefined Delegates for Performance

    #region Cached EventIds

    // Cached EventIds reduce allocations when structured logging is used.
    private static readonly EventId _traceId = new((int)LogLevel.Trace, "TraceEvent");
    private static readonly EventId _debugId = new((int)LogLevel.Debug, "DebugEvent");
    private static readonly EventId _infoId = new((int)LogLevel.Information, "InformationEvent");
    private static readonly EventId _warnId = new((int)LogLevel.Warning, "WarningEvent");
    private static readonly EventId _errorId = new((int)LogLevel.Error, "ErrorEvent");
    private static readonly EventId _criticalId = new((int)LogLevel.Critical, "CriticalEvent");

    /// <summary>
    /// Retrieves a cached <see cref="EventId"/> for the specified log level.
    /// </summary>
    private static EventId GetEventId(LogLevel level) => level switch
    {
        LogLevel.Trace => _traceId,
        LogLevel.Debug => _debugId,
        LogLevel.Information => _infoId,
        LogLevel.Warning => _warnId,
        LogLevel.Error => _errorId,
        LogLevel.Critical => _criticalId,
        _ => new EventId(0, "UnknownEvent")
    };

    #endregion Cached EventIds

    #region Generic Log Method

    /// <summary>
    /// <para>Core log method used by all convenience wrappers.</para>
    /// <para>
    /// Uses a two-path strategy:
    /// - Fast path: when no <paramref name="args"/> are supplied,
    ///   uses precompiled delegates (zero allocation).
    /// - Fallback: when arguments are present, logs with cached <see cref="EventId"/>.
    /// </para>
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        message ??= "N/A";

        if (!logger.IsEnabled(level))
        {
            return;
        }

        if (_delegates.TryGetValue(level, out var del))
        {
            if (args == null)
            {
                // Fast path: no args provided, zero-allocation delegate
                del(logger, message, exception);
                return;
            }
            else if (args.Length == 0)
            {
                // Args provided but empty → pass Array.Empty<object>() to avoid null checks internally
                logger.Log(level, GetEventId(level), exception, message, Array.Empty<object>());
                return;
            }
        }

        // Regular structured logging with actual args
        logger.Log(level, GetEventId(level), exception, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Overload for logging without an <see cref="Exception"/>.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, params object[] args) =>
        Log(logger, level, message, null, args);

    #endregion Generic Log Method

    #region Convenience Methods

    // These are syntactic sugar around the generic log method,
    // providing strongly-typed entry points for each log level.

    public static void LogTrace(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Trace, message, args);

    public static void LogDebug(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Debug, message, args);

    public static void LogInformation(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Information, message, args);

    public static void LogWarning(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Warning, message, args);

    public static void LogError(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Error, message, exception, args);

    public static void LogError(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Error, message, null, args);

    public static void LogCritical(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Critical, message, exception, args);

    #endregion Convenience Methods

    #region Exception Logging

    // Pooling StringBuilder to avoid repeated allocations
    // when formatting exception details.
    private static readonly ObjectPool<StringBuilder> _sbPool =
        new DefaultObjectPoolProvider().CreateStringBuilderPool();

    /// <summary>
    /// <para>Logs a detailed exception report (timestamp, type, message, inner exception, stack trace, source).</para>
    /// <para>Note: does not pass the exception object directly to avoid duplicate stack traces in logs.</para>
    /// <param>An optional title to prepend to the log entry (default: "Internal System Error").</param>
    /// </summary>
    public static void LogException(ILogger logger, Exception ex, string title = "Internal System Error")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        var msg = FormatExceptionMessage(ex, title);

        _error(logger, msg, null);
    }

    /// <summary>
    /// Formats an exception into a structured, human-readable string.
    /// Uses <see cref="StringBuilder"/> pooling to reduce GC pressure.
    /// </summary>
    private static string FormatExceptionMessage(Exception ex, string title)
    {
        var sb = _sbPool.Get();
        try
        {
            _ = sb.Clear();
            _ = sb.Append("Timestamp      : ").AppendLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                  .Append("Title          : ").AppendLine(title)
                  .Append("Exception Type : ").AppendLine(ex.GetType().FullName)
                  .Append("Exception      : ").AppendLine(ex.Message?.Trim() ?? "N/A")
                  .AppendLine()
                  .Append("Inner Exception: ").AppendLine(ex.InnerException?.Message?.Trim() ?? "N/A")
                  .Append("Stack Trace    : ").AppendLine(ex.StackTrace?.Trim() ?? "N/A")
                  .AppendLine()
                  .Append("Source         : ").AppendLine(ex.Source ?? "N/A");

            return sb.ToString();
        }
        finally
        {
            _sbPool.Return(sb);
        }
    }

    #endregion Exception Logging

    #region Log Scope Helper

    /// <summary>
    /// Represents a single key/value scope, implemented as a struct
    /// to avoid dictionary allocations for simple scenarios.
    /// </summary>
    private readonly struct SingleScope : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly KeyValuePair<string, object> _pair;

        public SingleScope(string key, object value) =>
            _pair = new KeyValuePair<string, object>(key, value ?? "N/A");

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return _pair;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Begins a logging scope with a single key-value pair.
    /// Ensures context (e.g., RequestId) is automatically attached to all logs inside the scope.
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Key cannot be null or whitespace.", nameof(key))
            : logger.BeginScope(new SingleScope(key, value)) ?? NullScope.Instance;
    }

    /// <summary>
    /// Begins a logging scope with multiple key-value pairs.
    /// Null-safe: if dictionary is null or empty, returns a no-op scope.
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, IDictionary<string, object> context)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (context is null || context.Count == 0)
        {
            return NullScope.Instance;
        }

        // Ensure values are not null (replace with "N/A")
        var safe = new Dictionary<string, object>(context.Count);
        foreach (var kv in context)
        {
            safe[kv.Key] = kv.Value ?? "N/A";
        }

        return logger.BeginScope(safe) ?? NullScope.Instance;
    }

    #endregion Log Scope Helper
}

/// <summary>
/// A disposable no-op logging scope used when no real scope is required.
/// Prevents null checks on scope operations.
/// <para>
/// This class is used internally by <see cref="ExLogger"/> to provide a default, no-operation
/// implementation of <see cref="IDisposable"/> for logging scopes when no actual scope is needed.
/// </para>
/// </summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    private NullScope()
    { }

    public void Dispose()
    { }
}