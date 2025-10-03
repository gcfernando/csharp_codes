using System.Collections;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

/*
 * Developer ::> Gehan Fernando
 * Date      ::> 15-Sep-2025
*/

namespace LogFunction.Logger;

/// <summary>
/// High-performance logging helper that extends <see cref="ILogger"/> functionality.
/// <para>
/// Features:
/// - Predefined delegates for fast-path logging (minimizes allocations).
/// - Structured logging support (preserves {Placeholders}).
/// - Exception logging with recursive inner exception details.
/// - Configurable exception formatting (global and per-call).
/// - Logging scopes with key-value context (single or multiple).
/// - Reuses pooled <see cref="StringBuilder"/> instances for performance.
/// </para>
/// </summary>
public static class ExLogger
{
    #region Predefined Delegates for Performance

    // Predefined static delegates (cached) for each log level.
    // These reduce allocations compared to calling logger.Log directly.
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

    // Map each LogLevel to its delegate for quick lookup
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

    // Cached EventId instances for each log level (avoids repeated allocations).
    private static readonly EventId _traceId = new((int)LogLevel.Trace, "TraceEvent");
    private static readonly EventId _debugId = new((int)LogLevel.Debug, "DebugEvent");
    private static readonly EventId _infoId = new((int)LogLevel.Information, "InformationEvent");
    private static readonly EventId _warnId = new((int)LogLevel.Warning, "WarningEvent");
    private static readonly EventId _errorId = new((int)LogLevel.Error, "ErrorEvent");
    private static readonly EventId _criticalId = new((int)LogLevel.Critical, "CriticalEvent");

    /// <summary>
    /// Returns a cached <see cref="EventId"/> for the given log level.
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

    #region Generic Log Methods

    /// <summary>
    /// Generic log method that chooses the fastest available logging path.
    /// Falls back to structured logging if arguments are provided.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        message ??= "N/A";

        if (!logger.IsEnabled(level))
        {
            return;
        }

        // Fast-path: plain messages with no structured arguments
        if ((args is null || args.Length == 0) && _delegates.TryGetValue(level, out var del))
        {
            del(logger, message, exception);
            return;
        }

        // Structured/fallback path: supports {Placeholders} and args
        logger.Log(level, GetEventId(level), exception, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Overload: log without an exception object.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, params object[] args) =>
        Log(logger, level, message, null, args);

    /// <summary>
    /// Overload: log with structured message template.
    /// This overload is required to preserve named placeholders ({UserId}, {OrderId}).
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, Exception exception, string messageTemplate, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        messageTemplate ??= "N/A";

        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, GetEventId(level), exception, messageTemplate, args ?? Array.Empty<object>());
    }

    #endregion Generic Log Methods

    #region Convenience Methods

    // Shorthand wrappers for common log levels.
    // These reduce boilerplate for typical logging scenarios.

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

    // Reusable pool of StringBuilders to minimize allocations when formatting exceptions
    private static readonly ObjectPool<StringBuilder> _sbPool =
        new DefaultObjectPoolProvider { MaximumRetained = 128 }.CreateStringBuilderPool();

    /// <summary>
    /// Global formatter function for exceptions.
    /// Clients may override this to implement custom formatting (e.g., JSON).
    /// </summary>
    public static Func<Exception, string, bool, string> ExceptionFormatter { get; set; } =
        DefaultFormatExceptionMessage;

    /// <summary>
    /// Logs an exception with structured details at Error level.
    /// Uses the configured <see cref="ExceptionFormatter"/>.
    /// </summary>
    public static void LogErrorException(ILogger logger, Exception ex, string title = "System Error", bool moreDetailsEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        var msg = ExceptionFormatter(ex, title, moreDetailsEnabled);
        Log(logger, LogLevel.Error, msg, ex);
    }

    /// <summary>
    /// Logs an exception with structured details at Critical level.
    /// Uses the configured <see cref="ExceptionFormatter"/>.
    /// </summary>
    public static void LogCriticalException(ILogger logger, Exception ex, string title = "Critical System Error", bool moreDetailsEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Critical))
        {
            return;
        }

        var msg = ExceptionFormatter(ex, title, moreDetailsEnabled);
        Log(logger, LogLevel.Critical, msg, ex);
    }

    /// <summary>
    /// Logs an exception with a one-off custom formatter instead of the global formatter.
    /// </summary>
    public static void LogExceptionWithFormatter(ILogger logger, Exception ex, LogLevel level,
        Func<Exception, string, bool, string> formatter, string title, bool moreDetailsEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(formatter);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        var msg = formatter(ex, title, moreDetailsEnabled);
        Log(logger, level, msg, ex);
    }

    /// <summary>
    /// Default exception formatting implementation.
    /// Includes timestamp, type, message, HResult, source, target site, stack trace, and inner exceptions.
    /// </summary>
    private static string DefaultFormatExceptionMessage(Exception ex, string title, bool moreDetailsEnabled)
    {
        var sb = _sbPool.Get();
        try
        {
            _ = sb.Clear();

            _ = sb.AppendLine($"Timestamp      : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC")
                  .AppendLine($"Title          : {title}")
                  .AppendLine($"Exception Type : {ex.GetType().FullName}")
                  .AppendLine($"Message        : {ex.Message?.Trim() ?? "N/A"}")
                  .AppendLine($"HResult        : {ex.HResult}")
                  .AppendLine($"Source         : {ex.Source ?? "N/A"}")
                  .AppendLine($"Target Site    : {ex.TargetSite?.Name ?? "N/A"}");

            if (moreDetailsEnabled)
            {
                _ = sb.AppendLine()
                      .AppendLine("Stack Trace    :")
                      .AppendLine(ex.StackTrace?.Trim() ?? "N/A");

                // Recursively log inner exceptions
                if (ex.InnerException is not null)
                {
                    _ = sb.AppendLine()
                          .AppendLine("---- Inner Exceptions ----");
                    AppendInnerExceptionDetails(sb, ex.InnerException, 1);
                }
            }

            return sb.ToString();
        }
        finally
        {
            _sbPool.Return(sb);
        }
    }

    /// <summary>
    /// Recursively appends details of inner exceptions (with indentation and depth limit).
    /// </summary>
    private static void AppendInnerExceptionDetails(StringBuilder sb, Exception inner, int depth, int maxDepth = 10)
    {
        if (inner is null || depth > maxDepth)
        {
            return;
        }

        var indent = new string('>', depth);

        _ = sb.AppendLine($"{indent} Exception Type : {inner?.GetType().FullName}")
              .AppendLine($"{indent} Message        : {inner?.Message?.Trim() ?? "N/A"}")
              .AppendLine($"{indent} HResult        : {inner?.HResult}")
              .AppendLine($"{indent} Source         : {inner?.Source ?? "N/A"}")
              .AppendLine($"{indent} Target Site    : {inner?.TargetSite?.Name ?? "N/A"}");

        if (!string.IsNullOrWhiteSpace(inner.StackTrace))
        {
            _ = sb.AppendLine($"{indent} Stack Trace    :")
                  .AppendLine(inner.StackTrace.Trim());
        }

        // Special handling: unwrap AggregateException with multiple inner exceptions
        if (inner is AggregateException agg && agg.InnerExceptions.Count > 0)
        {
            foreach (var sub in agg.InnerExceptions)
            {
                _ = sb.AppendLine();
                AppendInnerExceptionDetails(sb, sub, depth + 1, maxDepth);
            }
        }
        else if (inner.InnerException is not null)
        {
            _ = sb.AppendLine();
            AppendInnerExceptionDetails(sb, inner.InnerException, depth + 1, maxDepth);
        }
    }

    #endregion Exception Logging

    #region Log Scope Helper

    /// <summary>
    /// Begins a structured logging scope with a single key-value pair.
    /// Useful for correlating logs (e.g., RequestId).
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Key cannot be null or whitespace.", nameof(key))
            : logger.BeginScope(new SingleScope(key, value)) ?? NullScope.Instance;
    }

    /// <summary>
    /// Begins a structured logging scope with multiple key-value pairs.
    /// Useful for passing contextual metadata to all logs within the scope.
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, IDictionary<string, object> context)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (context is null || context.Count == 0)
        {
            return NullScope.Instance;
        }

        // Replace null values with "N/A" to avoid issues in structured logging
        var safe = new List<KeyValuePair<string, object>>(context.Count);
        foreach (var kv in context)
        {
            safe.Add(new KeyValuePair<string, object>(kv.Key, kv.Value ?? "N/A"));
        }

        return logger.BeginScope(new ScopeWrapper(safe)) ?? NullScope.Instance;
    }

    /// <summary>
    /// Represents a scope containing a single key-value pair.
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

        public override string ToString() => $"{_pair.Key}={_pair.Value}";
    }

    /// <summary>
    /// Represents a scope with multiple key-value pairs.
    /// Expands correctly in loggers like Console and Application Insights.
    /// </summary>
    private sealed class ScopeWrapper : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> _items;

        public ScopeWrapper(List<KeyValuePair<string, object>> items) => _items = items;

        public int Count => _items.Count;
        public KeyValuePair<string, object> this[int index] => _items[index];

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public override string ToString() =>
            string.Join(" ", _items.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    #endregion Log Scope Helper
}

/// <summary>
/// Represents a no-op scope when structured logging is not required.
/// Prevents null reference issues when BeginScope returns null.
/// </summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    private NullScope() { }

    public void Dispose() { }
}
