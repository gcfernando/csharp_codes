using System.Text;
using Microsoft.Extensions.Logging;

// Developer ::> Gehan Fernando
// Date      ::> 15-Sep-2025

namespace LogFunction;

/// <summary>
/// A centralized static logger helper that provides strongly-typed
/// and consistent logging methods across different log levels.
/// Optimized for production with structured logging, robust exception handling,
/// and contextual log scopes.
/// </summary>
public static class ExLogger
{
    #region Predefined Delegates for Performance

    private static readonly Action<ILogger, string, Exception> _logTraceDelegate =
        LoggerMessage.Define<string>(LogLevel.Trace, new EventId(0, "TraceEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _logDebugDelegate =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "DebugEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _logInformationDelegate =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "InformationEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _logWarningDelegate =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, "WarningEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _logErrorDelegate =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ErrorEvent"), "{Message}");

    private static readonly Action<ILogger, string, Exception> _logCriticalDelegate =
        LoggerMessage.Define<string>(LogLevel.Critical, new EventId(5, "CriticalEvent"), "{Message}");

    #endregion Predefined Delegates for Performance

    #region Log Delegates Dictionary

    private static readonly Dictionary<LogLevel, Action<ILogger, string, Exception>> _logActions =
        new()
        {
            [LogLevel.Trace] = _logTraceDelegate,
            [LogLevel.Debug] = _logDebugDelegate,
            [LogLevel.Information] = _logInformationDelegate,
            [LogLevel.Warning] = _logWarningDelegate,
            [LogLevel.Error] = _logErrorDelegate,
            [LogLevel.Critical] = _logCriticalDelegate
        };

    #endregion Log Delegates Dictionary

    #region Generic Log Method

    public static void Log(ILogger logger, LogLevel logLevel, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);

        // Null-safe logging
        message ??= "N/A";

        if (!logger.IsEnabled(logLevel))
        {
            return;
        }

        if (_logActions.TryGetValue(logLevel, out var logDelegate))
        {
            if (args?.Length > 0)
            {
                // Structured logging with EventId
                logger.Log(logLevel, new EventId((int)logLevel, $"{logLevel}Event"), exception, message, args);
            }
            else
            {
                // Predefined delegate logging
                logDelegate(logger, message, exception);
            }
        }
        else
        {
            // Fallback with default EventId
            logger.Log(logLevel, new EventId(0, "UnknownEvent"), exception, "{Message}", message);
        }
    }

    public static void Log(ILogger logger, LogLevel logLevel, string message, params object[] args) =>
        Log(logger, logLevel, message, null, args);

    #endregion Generic Log Method

    #region Convenience Methods

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

    public static void LogException(ILogger logger, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        const string errorTitle = "Internal System Error";
        var errorMessage = FormatExceptionMessage(ex, errorTitle);

        // Log as Error without passing exception to avoid duplicate stack trace
        _logErrorDelegate(logger, errorMessage.Trim(), null);
    }

    private static string FormatExceptionMessage(Exception ex, string title)
    {
        var sb = new StringBuilder()
            .Append("Timestamp      : ").AppendLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
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

    #endregion Exception Logging

    #region Log Scope Helper

    /// <summary>
    /// Begins a logging scope with a single key-value pair.
    /// Contextual info (like RequestId) is included in all logs inside the scope.
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Key cannot be null or whitespace.", nameof(key))
            : logger.BeginScope(new Dictionary<string, object> { [key] = value ?? "N/A" });
    }

    /// <summary>
    /// Begins a logging scope with multiple key-value pairs.
    /// </summary>
    public static IDisposable BeginScope(ILogger logger, IDictionary<string, object> context)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (context == null || context.Count == 0)
        {
            return null;
        }

        var safeContext = new Dictionary<string, object>();
        foreach (var kvp in context)
        {
            safeContext[kvp.Key] = kvp.Value ?? "N/A";
        }

        return logger.BeginScope(safeContext);
    }

    #endregion Log Scope Helper
}