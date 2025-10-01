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
/// Provides extension-based logging utilities with optimized delegates,
/// structured exception logging, and scoped context support.
/// </summary>
public static class ExLogger
{
    #region Predefined Delegates for Performance

    // Predefined logging delegates for each log level.
    // These are cached for performance to reduce runtime allocations.
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

    // Dictionary mapping log levels to their respective delegates.
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

    // Cached EventIds for each log level to avoid repeated allocations.
    private static readonly EventId _traceId = new((int)LogLevel.Trace, "TraceEvent");
    private static readonly EventId _debugId = new((int)LogLevel.Debug, "DebugEvent");
    private static readonly EventId _infoId = new((int)LogLevel.Information, "InformationEvent");
    private static readonly EventId _warnId = new((int)LogLevel.Warning, "WarningEvent");
    private static readonly EventId _errorId = new((int)LogLevel.Error, "ErrorEvent");
    private static readonly EventId _criticalId = new((int)LogLevel.Critical, "CriticalEvent");

    /// <summary>
    /// Returns a cached <see cref="EventId"/> for the given <see cref="LogLevel"/>.
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
    /// Logs a message with the specified log level, message, exception, and arguments.
    /// Uses cached delegates where possible for performance optimization.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        message ??= "N/A"; // Default to "N/A" if message is null

        if (!logger.IsEnabled(level))
        {
            return; // Skip if logging is disabled for this level
        }

        if (_delegates.TryGetValue(level, out var del))
        {
            if (args == null)
            {
                // No arguments provided, log directly using delegate
                del(logger, message, exception);
                return;
            }
            else if (args.Length == 0)
            {
                // Empty argument list, log with empty object array
                logger.Log(level, GetEventId(level), exception, message, Array.Empty<object>());
                return;
            }
        }

        // Fallback: log with provided args (if not null)
        logger.Log(level, GetEventId(level), exception, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Overload: Logs a message with no exception.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, params object[] args) =>
        Log(logger, level, message, null, args);

    #endregion Generic Log Method

    #region Convenience Methods

    /// <summary> Logs a trace message. </summary>
    public static void LogTrace(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Trace, message, args);

    /// <summary> Logs a debug message. </summary>
    public static void LogDebug(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Debug, message, args);

    /// <summary> Logs an information message. </summary>
    public static void LogInformation(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Information, message, args);

    /// <summary> Logs a warning message. </summary>
    public static void LogWarning(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Warning, message, args);

    /// <summary> Logs an error message with exception. </summary>
    public static void LogError(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Error, message, exception, args);

    /// <summary> Logs an error message without exception. </summary>
    public static void LogError(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Error, message, null, args);

    /// <summary> Logs a critical message with exception. </summary>
    public static void LogCritical(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Critical, message, exception, args);

    #endregion Convenience Methods

    #region Exception Logging

    // Object pool to reuse StringBuilder instances for exception formatting.
    private static readonly ObjectPool<StringBuilder> _sbPool =
        new DefaultObjectPoolProvider().CreateStringBuilderPool();

    /// <summary>
    /// Logs an exception with detailed structured information (timestamp, type, stack trace).
    /// </summary>
    public static void LogException(ILogger logger, Exception ex, string title = "Internal System Error", bool moreDetailsEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Error))
        {
            return; // Skip if error logging is disabled
        }

        var msg = FormatExceptionMessage(ex, title, moreDetailsEnabled);

        _error(logger, msg, null);
    }

    /// <summary>
    /// Builds a detailed exception log message using a pooled <see cref="StringBuilder"/>.
    /// </summary>
    private static string FormatExceptionMessage(Exception ex, string title, bool moreDetailsEnabled)
    {
        var sb = _sbPool.Get();
        try
        {
            sb.Clear();

            // Append structured details
            sb.Append("Timestamp      : ").AppendLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
              .Append("Title          : ").AppendLine(title)
              .Append("Exception Type : ").AppendLine(ex.GetType().FullName)
              .Append("Exception      : ").AppendLine(ex.Message?.Trim() ?? "N/A")
              .AppendLine();

            if (moreDetailsEnabled)
            {
                sb.Append("Inner Exception: ").AppendLine(ex.InnerException?.Message?.Trim() ?? "N/A")
                  .Append("Stack Trace    : ").AppendLine(ex.StackTrace?.Trim() ?? "N/A")
                  .AppendLine();
            }

            sb.Append("Source         : ").AppendLine(ex.Source ?? "N/A");

            return sb.ToString();
        }
        finally
        {
            _sbPool.Return(sb); // Return builder to pool
        }
    }

    #endregion Exception Logging

    #region Log Scope Helper

    /// <summary>
    /// Represents a single key-value scope for logging.
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
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, IDictionary<string, object> context)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (context is null || context.Count == 0)
        {
            return NullScope.Instance;
        }

        // Ensure no null values exist in scope
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
/// Represents a no-op logging scope (when no scope is active).
/// </summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    private NullScope()
    { }

    public void Dispose()
    { }
}