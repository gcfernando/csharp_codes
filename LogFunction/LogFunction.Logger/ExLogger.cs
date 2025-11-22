using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

/*
 * Developer ::> Gehan Fernando
 * Date      ::> 15-Sep-2025
 */

namespace LogFunction.Logger;

public static class ExLogger
{
    private const string _message = "{Message}";
    static ExLogger()
    {
        AppDomain.CurrentDomain.ProcessExit += static (_, __) => SafeShutdown();
        AppDomain.CurrentDomain.DomainUnload += static (_, __) => SafeShutdown();
    }

    #region Predefined Delegates for Performance

    private static readonly Action<ILogger, string, Exception> _trace =
        LoggerMessage.Define<string>(LogLevel.Trace, new EventId(0, "TraceEvent"), _message);

    private static readonly Action<ILogger, string, Exception> _debug =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "DebugEvent"), _message);

    private static readonly Action<ILogger, string, Exception> _info =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "InformationEvent"), _message);

    private static readonly Action<ILogger, string, Exception> _warn =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, "WarningEvent"), _message);

    private static readonly Action<ILogger, string, Exception> _error =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ErrorEvent"), _message);

    private static readonly Action<ILogger, string, Exception> _critical =
        LoggerMessage.Define<string>(LogLevel.Critical, new EventId(5, "CriticalEvent"), _message);

    private static readonly Action<ILogger, string, Exception> _noop = static (_, __, ___) => { };

    private static readonly Action<ILogger, string, Exception>[] _byLevel =
    [
        _trace,
        _debug,
        _info,
        _warn,
        _error,
        _critical,
        _noop
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Action<ILogger, string, Exception> Resolve(LogLevel level)
    {
        var idx = (int)level;
        return (uint)idx < (uint)_byLevel.Length ? _byLevel[idx] : _info;
    }

    #endregion Predefined Delegates for Performance

    #region Cached EventIds

    private static readonly EventId _traceId = new(1, "TraceEvent");
    private static readonly EventId _debugId = new(2, "DebugEvent");
    private static readonly EventId _infoId = new(3, "InformationEvent");
    private static readonly EventId _warnId = new(4, "WarningEvent");
    private static readonly EventId _errorId = new(5, "ErrorEvent");
    private static readonly EventId _criticalId = new(6, "CriticalEvent");
    private static readonly EventId _unknownId = new(9999, "UnknownEvent");

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static EventId GetEventId(LogLevel level) => level switch
    {
        LogLevel.Trace => _traceId,
        LogLevel.Debug => _debugId,
        LogLevel.Information => _infoId,
        LogLevel.Warning => _warnId,
        LogLevel.Error => _errorId,
        LogLevel.Critical => _criticalId,
        _ => _unknownId
    };

    #endregion Cached EventIds

    #region Timestamp Cache (Allocation-Free UTC)

    private static readonly ThreadLocal<char[]> _utcBuffer = new(() => new char[33]);
    private static readonly Timer _utcCacheTimer = new(UpdateUtc, null, 0, 1);
    private static volatile string _cachedUtc = FormatUtc();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void UpdateUtc(object _) =>
        Interlocked.Exchange(ref _cachedUtc, FormatUtc());

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static string FormatUtc()
    {
        var buf = _utcBuffer?.Value ?? new char[33];
        _ = DateTime.UtcNow.TryFormat(buf, out var len, "O", CultureInfo.InvariantCulture);
        return new string(buf, 0, len);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShutdownUtcTimer()
    {
        try
        {
            _ = Interlocked.Exchange(ref _cachedUtc, FormatUtc());
            _ = _utcCacheTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _utcCacheTimer.Dispose();
        }
        catch (ObjectDisposedException)
        {
            /* Exception intentionally ignored; no corrective action required. */
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SafeShutdown()
    {
        try
        {
            ShutdownUtcTimer();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            Debug.WriteLine($"ExLogger.SafeShutdown encountered non-fatal error: {ex}");
        }
    }

    #endregion Timestamp Cache (Allocation-Free UTC)

    #region Object Pool & Exception Formatter

    private static readonly ObjectPoolProvider _poolProvider = new DefaultObjectPoolProvider
    {
        MaximumRetained = Environment.ProcessorCount * 8
    };

    private static readonly ObjectPool<StringBuilder> _sbPool = _poolProvider.CreateStringBuilderPool();
    private static volatile Func<Exception, string, bool, string> _exceptionFormatter = FormatExceptionMessageInternal;

    public static Func<Exception, string, bool, string> ExceptionFormatter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _exceptionFormatter;
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set => _exceptionFormatter = value ?? FormatExceptionMessageInternal;
    }

    #endregion Object Pool & Exception Formatter

    #region Generic Log Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log(ILogger logger, LogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        Resolve(level)(logger, message ?? "N/A", null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log<T1>(ILogger logger, LogLevel level, string message, T1 arg1)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, GetEventId(level), null, message ?? "N/A", arg1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log<T1, T2>(ILogger logger, LogLevel level, string message, T1 arg1, T2 arg2)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, GetEventId(level), null, message ?? "N/A", arg1, arg2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log(ILogger logger, LogLevel level, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        message ??= "N/A";
        if (args is null || args.Length == 0)
        {
            Resolve(level)(logger, message, exception);
            return;
        }

        logger.Log(level, GetEventId(level), exception, message, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log(ILogger logger, LogLevel level, string message, params object[] args) =>
        Log(logger, level, message, null, args);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log(ILogger logger, LogLevel level, Exception exception, string messageTemplate, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(exception);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        messageTemplate ??= "N/A";
        logger.Log(level, GetEventId(level), exception, messageTemplate, args ?? Array.Empty<object>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void LogNoArgs(ILogger logger, LogLevel level, string message)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        Resolve(level)(logger, message ?? "N/A", null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void LogNoArgs(ILogger logger, LogLevel level, string message, Exception exception)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        Resolve(level)(logger, message ?? "N/A", exception);
    }

    #endregion Generic Log Methods

    #region Convenience Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogTrace(this ILogger logger, string message, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Trace, message); return; }
        Log(logger, LogLevel.Trace, message, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogDebug(this ILogger logger, string message, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Debug, message); return; }
        Log(logger, LogLevel.Debug, message, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogInformation(this ILogger logger, string message, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Information, message); return; }
        Log(logger, LogLevel.Information, message, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogWarning(this ILogger logger, string message, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Warning, message); return; }
        Log(logger, LogLevel.Warning, message, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogError(this ILogger logger, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(exception);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Error, message, exception); return; }
        Log(logger, LogLevel.Error, message, exception, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogError(this ILogger logger, string message, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Error, message); return; }
        Log(logger, LogLevel.Error, message, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogCritical(this ILogger logger, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(exception);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Critical, message, exception); return; }
        Log(logger, LogLevel.Critical, message, exception, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExLogCritical(this ILogger logger, string message, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (args is null || args.Length == 0)
        { LogNoArgs(logger, LogLevel.Critical, message); return; }
        Log(logger, LogLevel.Critical, message, args);
    }

    #endregion Convenience Methods

    #region Exception Logging

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ExLogErrorException(this ILogger logger, Exception ex, string title = "System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        var msg = _exceptionFormatter(ex, title, moreDetailsEnabled);
        LogNoArgs(logger, LogLevel.Error, msg, null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ExLogCriticalException(this ILogger logger, Exception ex, string title = "Critical System Error", bool moreDetailsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Critical))
        {
            return;
        }

        var msg = _exceptionFormatter(ex, title, moreDetailsEnabled);
        LogNoArgs(logger, LogLevel.Critical, msg, null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string FormatExceptionMessageInternal(Exception ex, string title, bool moreDetailsEnabled)
    {
        var sb = _sbPool.Get();
        try
        {
            _ = sb.Clear();
            _ = sb.EnsureCapacity(1024);

            _ = sb.Append("Timestamp      : ").AppendLine(_cachedUtc)
                .Append("Title          : ").AppendLine(title ?? "N/A")
                .Append("Exception Type : ").AppendLine(ex.GetType().FullName)
                .Append("Message        : ").AppendLine(ex.Message?.Trim() ?? "N/A")
                .Append("HResult        : ").Append(ex.HResult).AppendLine()
                .Append("Source         : ").AppendLine(ex.Source ?? "N/A")
                .Append("Target Site    : ").AppendLine(ex.TargetSite?.Name ?? "N/A");

            if (moreDetailsEnabled)
            {
                var st = ex.StackTrace;
                if (!string.IsNullOrWhiteSpace(st))
                {
                    _ = sb.AppendLine().AppendLine("Stack Trace    :").AppendLine(st.Trim());
                }

                if (ex.InnerException is not null)
                {
                    _ = sb.AppendLine().AppendLine("---- Inner Exceptions ----");
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AppendInnerExceptionDetails(StringBuilder sb, Exception inner, int depth, int maxDepth = 5)
    {
        if (inner is null || depth > maxDepth)
        {
            return;
        }

        var indent = new string('>', depth);
        _ = sb.Append(indent).Append(" Exception Type : ").AppendLine(inner.GetType().FullName)
            .Append(indent).Append(" Message        : ").AppendLine(inner.Message?.Trim() ?? "N/A")
            .Append(indent).Append(" HResult        : ").Append(inner.HResult).AppendLine()
            .Append(indent).Append(" Source         : ").AppendLine(inner.Source ?? "N/A")
            .Append(indent).Append(" Target Site    : ").AppendLine(inner.TargetSite?.Name ?? "N/A");

        var st = inner.StackTrace;
        if (!string.IsNullOrWhiteSpace(st))
        {
            _ = sb.Append(indent).AppendLine(" Stack Trace    :").AppendLine(st.Trim());
        }

        if (inner is AggregateException agg && agg.InnerExceptions.Count > 0)
        {
            for (var i = 0; i < agg.InnerExceptions.Count; i++)
            {
                _ = sb.AppendLine();
                AppendInnerExceptionDetails(sb, agg.InnerExceptions[i], depth + 1, maxDepth);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static IDisposable ExBeginScope(this ILogger logger, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        var scope = new SingleScope(key, value);
        return logger.BeginScope(scope) ?? new DisposableScope(scope);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static IDisposable ExBeginScope(this ILogger logger, IDictionary<string, object> context)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (context is null || context.Count == 0)
        {
            return NullScope.Instance;
        }

        if (context.Count <= 4)
        {
            var items = new KeyValuePair<string, object>[context.Count];
            var i = 0;
            foreach (var kv in context)
            {
                items[i++] = new KeyValuePair<string, object>(kv.Key, kv.Value ?? "N/A");
            }

            var wrapper = new SmallScopeWrapper(items);
            return logger.BeginScope(wrapper) ?? new DisposableScope(wrapper);
        }
        else
        {
            var safe = new List<KeyValuePair<string, object>>(context.Count);
            foreach (var kv in context)
            {
                safe.Add(new KeyValuePair<string, object>(kv.Key, kv.Value ?? "N/A"));
            }

            var wrapper = new ScopeWrapper(safe);
            return logger.BeginScope(wrapper) ?? new DisposableScope(wrapper);
        }
    }

    private readonly struct SingleScope : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly KeyValuePair<string, object> _pair;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public SingleScope(string key, object value) =>
            _pair = new KeyValuePair<string, object>(key, value ?? "N/A");

        public int Count => 1;

        public KeyValuePair<string, object> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => index == 0
                ? _pair
                : throw new ArgumentOutOfRangeException(nameof(index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public Enumerator GetEnumerator() => new(_pair);

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public override string ToString() => $"{_pair.Key}={_pair.Value}";

        public struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private bool _moved;

            public Enumerator(KeyValuePair<string, object> value) => (Current, _moved) = (value, false);

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public bool MoveNext()
            {
                if (_moved)
                {
                    return false;
                }

                _moved = true;
                return true;
            }

            public KeyValuePair<string, object> Current { get; }
            readonly object IEnumerator.Current => Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public void Reset() => _moved = false;

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public readonly void Dispose()
            { }
        }
    }

    private sealed class ScopeWrapper : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> _items;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ScopeWrapper(List<KeyValuePair<string, object>> items) => _items = items;

        public int Count => _items.Count;
        public KeyValuePair<string, object> this[int index] => _items[index];

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public override string ToString()
        {
            if (_items.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(_items.Count * 16);
            for (var i = 0; i < _items.Count; i++)
            {
                if (i > 0)
                {
                    _ = sb.Append(' ');
                }

                var kv = _items[i];
                _ = sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return sb.ToString();
        }
    }

    private sealed class SmallScopeWrapper : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly KeyValuePair<string, object>[] _items;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public SmallScopeWrapper(KeyValuePair<string, object>[] items) => _items = items;

        public int Count => _items.Length;
        public KeyValuePair<string, object> this[int index] => _items[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public Enumerator GetEnumerator() => new(_items);

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly KeyValuePair<string, object>[] _arr;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public Enumerator(KeyValuePair<string, object>[] arr) => (_arr, _index) = (arr, -1);

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public bool MoveNext() => ++_index < _arr.Length;

            public readonly KeyValuePair<string, object> Current => _arr[_index];
            readonly object IEnumerator.Current => _arr[_index];

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public void Reset() => _index = -1;

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public readonly void Dispose()
            { }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public override string ToString()
        {
            if (_items.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(_items.Length * 16);
            for (var i = 0; i < _items.Length; i++)
            {
                if (i > 0)
                {
                    _ = sb.Append(' ');
                }

                var kv = _items[i];
                _ = sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return sb.ToString();
        }
    }

    private sealed class DisposableScope : IDisposable
    {
        private readonly object _state;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public DisposableScope(object state) => _state = state;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Dispose()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public override string ToString() => _state.ToString() ?? string.Empty;
    }

    #endregion Log Scope Helper

    #region Extensibility Hooks (Async/Batch Ready)

    public static Func<LogLevel, string, Exception, bool> AsyncSinkFilter { get; private set; } = static (_, _, _) => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void UseAsyncSinkProvider(Func<LogLevel, string, Exception, bool> filter)
        => AsyncSinkFilter = filter ?? AsyncSinkFilter;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
        catch (OperationCanceledException) { /* Exception intentionally ignored; no corrective action required. */ }
        catch (ObjectDisposedException) { /* Exception intentionally ignored; no corrective action required. */ }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            Debug.WriteLine($"ExLogger.FlushAsync encountered error: {ex}");
        }
    }

    #endregion Extensibility Hooks (Async/Batch Ready)
}