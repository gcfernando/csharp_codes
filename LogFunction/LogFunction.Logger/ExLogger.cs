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
/// ExLogger: A high-performance static logging helper for .NET applications.
/// <para>
/// ⚡ Optimized for throughput and low allocations, built on top of <see cref="ILogger"/>.
/// </para>
/// Features:
/// - Predefined logging delegates for each <see cref="LogLevel"/> (avoids allocations from string formatting).
/// - Cached <see cref="EventId"/> values to avoid runtime allocation.
/// - Structured logging support with {Placeholders}.
/// - Exception logging with configurable formatting (stack trace, inner exceptions).
/// - Scope support for contextual logging (single or multiple key-value pairs).
/// - Uses <see cref="ObjectPool{T}"/> to reuse <see cref="StringBuilder"/> for exception messages.
/// </summary>
public static class ExLogger
{
    #region Predefined Delegates for Performance

    // ⚡ Predefined delegates using LoggerMessage.Define<T>.
    // This avoids allocations compared to calling logger.Log directly with interpolated strings.
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

    // 🔑 Lookup dictionary mapping log levels to delegates for quick access
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

    // 🔒 Cached EventIds for each log level to avoid allocating new instances at runtime
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
    /// Generic log method.
    /// ⚡ Uses precompiled delegates for fast-path logging when there are no arguments.
    /// Falls back to structured logging when arguments are provided.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        message ??= "N/A";

        // ⛔ Skip if this log level is disabled
        if (!logger.IsEnabled(level))
        {
            return;
        }

        // ⚡ Fast-path: No arguments, use delegate
        if ((args is null || args.Length == 0) && _delegates.TryGetValue(level, out var del))
        {
            del(logger, message, exception);
            return;
        }

        // Fallback: structured logging with placeholders
        logger.Log(level, GetEventId(level), exception, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Overload: log without an exception object.
    /// </summary>
    public static void Log(ILogger logger, LogLevel level, string message, params object[] args) =>
        Log(logger, level, message, null, args);

    /// <summary>
    /// Overload: log with structured message template and exception.
    /// Preserves named placeholders ({UserId}, {OrderId}).
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

    // 🚀 Shorthand wrappers for common log levels (reduce boilerplate and improve readability).

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Trace"/>.
    /// Use this for very detailed diagnostic information (typically only enabled during development).
    /// </summary>
    public static void LogTrace(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Trace, message, args);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Debug"/>.
    /// Useful for debugging and tracing application flow without being as verbose as <see cref="LogTrace"/>.
    /// </summary>
    public static void LogDebug(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Debug, message, args);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Information"/>.
    /// Intended for general application flow, user actions, or significant lifecycle events.
    /// </summary>
    public static void LogInformation(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Information, message, args);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Warning"/>.
    /// Use this when something unexpected occurred or a non-critical issue needs attention.
    /// </summary>
    public static void LogWarning(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Warning, message, args);

    /// <summary>
    /// Logs a message and an exception at <see cref="LogLevel.Error"/>.
    /// Use this when an operation fails but the application can continue running.
    /// </summary>
    public static void LogError(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Error, message, exception, args);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Error"/>.
    /// Use this overload when you want to log an error without an exception object.
    /// </summary>
    public static void LogError(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Error, message, null, args);

    /// <summary>
    /// Logs a message and an exception at <see cref="LogLevel.Critical"/>.
    /// Use this for unrecoverable failures or conditions that require immediate attention.
    /// </summary>
    public static void LogCritical(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Critical, message, exception, args);

    #endregion Convenience Methods

    #region Exception Logging

    // ♻️ Pool of StringBuilders to reduce allocations when formatting exceptions
    private static readonly ObjectPool<StringBuilder> _sbPool =
        new DefaultObjectPoolProvider { MaximumRetained = 128 }.CreateStringBuilderPool();

    /// <summary>
    /// Global formatter for exception logs.
    /// Can be replaced with a custom formatter (e.g., JSON or structured format).
    /// </summary>
    public static Func<Exception, string, bool, string> ExceptionFormatter { get; set; } =
        DefaultFormatExceptionMessage;

    /// <summary>
    /// Logs an exception at Error level with formatted details.
    /// </summary>
    public static void LogErrorException(ILogger logger, Exception ex, string title = "System Error", bool moreDetailsEnabled = false)
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
    /// Logs an exception at Critical level with formatted details.
    /// </summary>
    public static void LogCriticalException(ILogger logger, Exception ex, string title = "Critical System Error", bool moreDetailsEnabled = false)
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
    /// Default exception formatter.
    /// Includes timestamp, type, message, HRESULT, stack trace, and inner exceptions.
    /// Uses pooled <see cref="StringBuilder"/> to reduce allocations.
    /// </summary>
    private static string DefaultFormatExceptionMessage(Exception ex, string title, bool moreDetailsEnabled)
    {
        var sb = _sbPool.Get();
        try
        {
            sb.Clear();

            // 📋 Basic exception info
            sb.AppendLine($"Timestamp      : {DateTime.UtcNow:O}")
              .AppendLine($"Title          : {title ?? "N/A"}")
              .AppendLine($"Exception Type : {ex.GetType().FullName}")
              .AppendLine($"Message        : {ex.Message?.Trim() ?? "N/A"}")
              .AppendLine($"HResult        : {ex.HResult}")
              .AppendLine($"Source         : {ex.Source ?? "N/A"}")
              .AppendLine($"Target Site    : {ex.TargetSite?.Name ?? "N/A"}");

            // 📋 Detailed info (optional)
            if (moreDetailsEnabled)
            {
                if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                {
                    sb.AppendLine()
                      .AppendLine("Stack Trace    :")
                      .AppendLine(ex.StackTrace.Trim());
                }

                if (ex.InnerException is not null)
                {
                    sb.AppendLine()
                      .AppendLine("---- Inner Exceptions ----");
                    AppendInnerExceptionDetails(sb, ex.InnerException, 1, maxDepth: 3);
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
    /// Recursively appends details of inner exceptions with indentation.
    /// Stops at maxDepth to avoid excessive logging in recursive exception chains.
    /// </summary>
    private static void AppendInnerExceptionDetails(StringBuilder sb, Exception inner, int depth, int maxDepth = 5)
    {
        if (inner is null || depth > maxDepth)
        {
            return;
        }

        var indent = new string('>', depth);

        sb.AppendLine($"{indent} Exception Type : {inner.GetType().FullName}")
          .AppendLine($"{indent} Message        : {inner.Message?.Trim() ?? "N/A"}")
          .AppendLine($"{indent} HResult        : {inner.HResult}")
          .AppendLine($"{indent} Source         : {inner.Source ?? "N/A"}")
          .AppendLine($"{indent} Target Site    : {inner.TargetSite?.Name ?? "N/A"}");

        if (!string.IsNullOrWhiteSpace(inner.StackTrace))
        {
            sb.AppendLine($"{indent} Stack Trace    :")
              .AppendLine(inner.StackTrace.Trim());
        }

        // Special case: AggregateException contains multiple inner exceptions
        if (inner is AggregateException agg && agg.InnerExceptions.Count > 0)
        {
            foreach (var sub in agg.InnerExceptions)
            {
                sb.AppendLine();
                AppendInnerExceptionDetails(sb, sub, depth + 1, maxDepth);
            }
        }
        else if (inner.InnerException is not null)
        {
            sb.AppendLine();
            AppendInnerExceptionDetails(sb, inner.InnerException, depth + 1, maxDepth);
        }
    }

    #endregion Exception Logging

    #region Log Scope Helper

    /// <summary>
    /// Begins a structured logging scope with a single key-value pair.
    /// Useful for correlating logs (e.g., RequestId, UserId).
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
    /// Optimized for small dictionaries (≤4 items) to reduce allocations.
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, IDictionary<string, object> context)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (context is null || context.Count == 0)
        {
            return NullScope.Instance;
        }

        // ⚡ Optimization: use array wrapper for small contexts
        if (context.Count <= 4)
        {
            var items = new KeyValuePair<string, object>[context.Count];
            var i = 0;
            foreach (var kv in context)
            {
                items[i++] = new KeyValuePair<string, object>(kv.Key, kv.Value ?? "N/A");
            }
            return logger.BeginScope(new SmallScopeWrapper(items)) ?? NullScope.Instance;
        }
        else
        {
            // Fallback: use List wrapper for larger contexts
            var safe = new List<KeyValuePair<string, object>>(context.Count);
            foreach (var kv in context)
            {
                safe.Add(new KeyValuePair<string, object>(kv.Key, kv.Value ?? "N/A"));
            }
            return logger.BeginScope(new ScopeWrapper(safe)) ?? NullScope.Instance;
        }
    }

    /// <summary>
    /// Represents a scope with a single key-value pair.
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
    /// Represents a scope with multiple key-value pairs using a List.
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

    /// <summary>
    /// Represents a scope optimized for small dictionaries (≤4 items).
    /// Uses an array instead of a List to minimize allocations.
    /// </summary>
    private sealed class SmallScopeWrapper : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly KeyValuePair<string, object>[] _items;

        public SmallScopeWrapper(KeyValuePair<string, object>[] items) => _items = items;

        public int Count => _items.Length;
        public KeyValuePair<string, object> this[int index] => _items[index];

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<string, object>>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public override string ToString() =>
            string.Join(" ", _items.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    #endregion Log Scope Helper
}

/// <summary>
/// Represents a no-op logging scope.
/// Prevents null reference exceptions when BeginScope() returns null.
/// </summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    private NullScope()
    { }

    public void Dispose()
    { }
}